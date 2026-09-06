using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CoNotes.Application.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.Redis;

namespace IntegrationTests;

/// <summary>
/// 驗證 SignalR 透過 Redis backplane 運作:兩個獨立的 API host(模擬 k8s 上的兩個 replica)
/// 各自維護自己的 in-memory connection 清單, 但都接同一個 Redis; 連到「replica 1」的連線
/// 送出的 update, 連到「replica 2」的連線也要收得到, 證明轉發沒有被侷限在單一 process 內。
///
/// 這裡另外真的啟動兩個 host, 跟其他 IntegrationTests 共用的 <see cref="IntegrationTestWebAppFactory"/>
/// 不一樣(那個只透過 ISender 直接呼叫, 從不真的過 HTTP), 所以另外做自己的 JWT 簽章設定,
/// 讓 SignalR 的 [Authorize] 能過。放進這個專案(而不是另外開一個測試專案)是因為
/// Davish.Result 的 ResultHttpOptions 是 process-wide、只能設定一次——這個專案裡沒有其他
/// 測試會真的送出 HTTP request, 所以不會撞到那個鎖; FunctionalTests 專案就會撞到, 已經試過。
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class NoteCollabHubReplicaTests : IAsyncLifetime
{
    private const string TestIssuer = "https://auth.test/realms/conotes";
    private const string TestAudience = "conotes-spa";

    private static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("integration-tests-signalr-signing-key-do-not-use-elsewhere"));

    private readonly IntegrationTestWebAppFactory _dbHost = new();
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:8-alpine").Build();
    private WebApplicationFactory<Program> _replica1 = null!;
    private WebApplicationFactory<Program> _replica2 = null!;

    public async Task InitializeAsync()
    {
        await _dbHost.InitializeAsync();
        await _redisContainer.StartAsync();

        _replica1 = _dbHost.WithWebHostBuilder(ConfigureReplica);
        _replica2 = _dbHost.WithWebHostBuilder(ConfigureReplica);
    }

    public async Task DisposeAsync()
    {
        await _replica1.DisposeAsync();
        await _replica2.DisposeAsync();
        await _dbHost.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    private void ConfigureReplica(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:ConnectionString", _redisContainer.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            // IntegrationTestWebAppFactory 的 base 設定把 IUserContext 換成 TestUserContext(固定回傳
            // 一個手動設定的 AppUserId, 不看真正的 JWT), 因為其他 IntegrationTests 都是直接用 ISender
            // 呼叫、沒有真的 HTTP request。這裡真的會走 JWT → SignalR [Authorize] 的完整流程, 需要換回
            // 真正的 UserContext, 兩個 replica 才會用同一套邏輯從 token 解析出同一個 AppUserId。
            services.AddScoped<IUserContext, CoNotes.Infrastructure.Identity.UserContext>();

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TestIssuer,
                    ValidateAudience = true,
                    ValidAudience = TestAudience,
                    ValidateLifetime = true,
                    IssuerSigningKey = SigningKey,
                };
            });
        });
    }

    private static string CreateToken(string keycloakSub)
    {
        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: [new Claim("sub", keycloakSub)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task GivenTwoApiReplicasBehindTheSameRedis_WhenOneReplicaSendsAnUpdate_ThenTheOtherReplicasConnectionReceivesIt()
    {
        var keycloakSub = Guid.NewGuid().ToString();
        var token = CreateToken(keycloakSub);

        var client1 = _replica1.CreateClient();
        client1.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var createResponse = await client1.PostAsJsonAsync("/api/v1/notes", new { Title = "Collab note", Content = "Content" });
        var created = await createResponse.Content.ReadFromJsonAsync<CreateNoteResponse>();
        var noteId = created!.NoteId;

        await using var connectionOnReplica1 = BuildHubConnection(_replica1, token);
        await using var connectionOnReplica2 = BuildHubConnection(_replica2, token);

        var receivedUpdateOnReplica2 = new TaskCompletionSource<byte[]>();
        connectionOnReplica2.On<byte[]>("ReceiveUpdate", update => receivedUpdateOnReplica2.TrySetResult(update));

        await connectionOnReplica1.StartAsync();
        await connectionOnReplica2.StartAsync();

        await connectionOnReplica1.InvokeAsync("JoinNoteAsync", noteId);
        await connectionOnReplica2.InvokeAsync("JoinNoteAsync", noteId);

        var sentUpdate = Encoding.UTF8.GetBytes("yjs-update-from-replica-1");
        await connectionOnReplica1.InvokeAsync("SendUpdateAsync", noteId, sentUpdate);

        var completed = await Task.WhenAny(receivedUpdateOnReplica2.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(receivedUpdateOnReplica2.Task, completed);
        Assert.Equal(sentUpdate, await receivedUpdateOnReplica2.Task);
    }

    [Fact]
    public async Task GivenUnrelatedUser_WhenJoiningChatHubNoteGroup_ThenAccessIsRejected()
    {
        var ownerSub = Guid.NewGuid().ToString();
        var unrelatedSub = Guid.NewGuid().ToString();
        var ownerToken = CreateToken(ownerSub);
        var unrelatedToken = CreateToken(unrelatedSub);

        var ownerClient = _replica1.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);
        var createResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/notes",
            new { Title = "Chat note", Content = "Content" }
        );
        var created = await createResponse.Content.ReadFromJsonAsync<CreateNoteResponse>();
        var noteId = created!.NoteId;

        var unrelatedClient = _replica1.CreateClient();
        unrelatedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", unrelatedToken);
        var provisionResponse = await unrelatedClient.PostAsync("/api/test/app-user", content: null);
        Assert.Equal(HttpStatusCode.OK, provisionResponse.StatusCode);

        await using var ownerConnection = BuildChatHubConnection(_replica1, ownerToken);
        await using var unrelatedConnection = BuildChatHubConnection(_replica2, unrelatedToken);
        await ownerConnection.StartAsync();
        await unrelatedConnection.StartAsync();

        await ownerConnection.InvokeAsync("JoinNoteAsync", noteId);
        var exception = await Assert.ThrowsAsync<HubException>(
            () => unrelatedConnection.InvokeAsync("JoinNoteAsync", noteId)
        );
        Assert.Contains("沒有權限", exception.Message);
    }

    [Fact]
    public async Task GivenTwoChatHubReplicas_WhenOneConnectionSendsMessage_ThenOtherConnectionReceivesIt()
    {
        var keycloakSub = Guid.NewGuid().ToString();
        var token = CreateToken(keycloakSub);
        var client = _replica1.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        await client.PostAsJsonAsync("/api/test/app-user/plan-tier", new { PlanTier = "ProMax" });
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/notes",
            new { Title = "Chat note", Content = "Content" }
        );
        var created = await createResponse.Content.ReadFromJsonAsync<CreateNoteResponse>();
        var noteId = created!.NoteId;

        await using var connectionOnReplica1 = BuildChatHubConnection(_replica1, token);
        await using var connectionOnReplica2 = BuildChatHubConnection(_replica2, token);
        var received = new TaskCompletionSource<ChatMessageResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        connectionOnReplica2.On<ChatMessageResponse>(
            "ReceiveMessage",
            message => received.TrySetResult(message)
        );

        await connectionOnReplica1.StartAsync();
        await connectionOnReplica2.StartAsync();
        await connectionOnReplica1.InvokeAsync("JoinNoteAsync", noteId);
        await connectionOnReplica2.InvokeAsync("JoinNoteAsync", noteId);

        var sent = await connectionOnReplica1.InvokeAsync<ChatMessageResponse>(
            "SendMessageAsync",
            noteId,
            "hello from replica 1"
        );

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(received.Task, completed);
        var receivedMessage = await received.Task;
        Assert.Equal(sent.ChatMessageId, receivedMessage.ChatMessageId);
        Assert.Equal("hello from replica 1", receivedMessage.Content);
    }

    private static HubConnection BuildHubConnection(WebApplicationFactory<Program> factory, string token)
    {
        var handler = factory.Server.CreateHandler();

        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, $"/hubs/notes?access_token={token}"), options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    private static HubConnection BuildChatHubConnection(WebApplicationFactory<Program> factory, string token)
    {
        var handler = factory.Server.CreateHandler();

        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, $"/hubs/chat?access_token={token}"), options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    private sealed record CreateNoteResponse(Guid NoteId);

    private sealed record ChatMessageResponse(
        Guid ChatMessageId,
        Guid NoteId,
        Guid? AuthorAppUserId,
        bool IsAiReply,
        string Content,
        DateTime CreatedAt
    );
}
