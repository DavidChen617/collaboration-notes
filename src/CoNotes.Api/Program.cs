using CoNotes.Api.BackgroundJobs;
using CoNotes.Api.Hubs;
using CoNotes.Application;
using CoNotes.Application.Abstractions;
using CoNotes.Application.AppUsers.Commands.Upsert;
using CoNotes.Domain.AppUsers;
using CoNotes.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHttpContextAccessor()
    .AddEndpoints()
    .AddApiVersionConfiguration(builder.Environment.ApplicationName)
    .AddCustomResultErrorTypeMap()
    .AddProblemDetailConfiguration()
    .AddAuthenticationConfiguration(builder.Configuration, builder.Environment)
    .AddCorsConfiguration(builder.Configuration)
    .AddSignalRConfiguration(builder.Configuration, builder.Environment);

builder.AddOpenTelemetryConfiguration();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<IChatMessageBroadcaster, ChatMessageBroadcaster>();
builder.Services.AddSingleton<AiReplyRequestQueue>();
builder.Services.AddSingleton<IAiReplyRequestQueue>(
    services => services.GetRequiredService<AiReplyRequestQueue>()
);
builder.Services.AddHostedService(
    services => services.GetRequiredService<AiReplyRequestQueue>()
);

var app = builder.Build();

app.MapOpenApi().WithDocumentPerVersion();

app.UseCors(CorsConfiguration.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok())
   .AllowAnonymous();

app.MapPost("/api/test/app-user", async (ISender sender, HttpContext httpContext, CancellationToken ct) =>
{
    var keycloakSub = httpContext.User.FindFirst("sub")?.Value;
    if (keycloakSub is null)
        return Results.Unauthorized();

    var result = await sender.SendAsync(new UpsertAppUserCommand(keycloakSub), ct);

    return result.ToOk();
})
.RequireAuthorization();

// 測試用途:直接把目前使用者的訂閱等級設成指定值, 跳過 PayPal 付款/兌換 code 的完整流程——
// 讓 FunctionalTests 能在不用真的走一次付款流程的情況下, 準備好「擁有者的訂閱等級是 X」這個前置狀態。
app.MapPost("/api/test/app-user/plan-tier", async (
    SetTestPlanTierRequest request,
    IUserContext userContext,
    IAppUserRepository appUserRepository,
    CancellationToken ct) =>
{
    var appUserId = await userContext.GetAppUserIdAsync(ct);
    var appUser = await appUserRepository.FindByIdAsync(appUserId, ct);
    if (appUser is null)
        return Results.NotFound();

    appUser.ApplyRedeemedPlanTier(Enum.Parse<PlanTier>(request.PlanTier));
    await appUserRepository.UpdateAsync(appUser, ct);

    return Results.Ok();
})
.RequireAuthorization();

app.MapEndpoints();
app.MapHub<NoteCollabHub>("/hubs/notes");
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

internal sealed record SetTestPlanTierRequest(string PlanTier);
