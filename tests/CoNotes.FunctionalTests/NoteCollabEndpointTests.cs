using System.Net;
using System.Net.Http.Json;
using CoNotes.FunctionalTests;

namespace FunctionalTests;

[Collection(nameof(ApiCollection))]
public sealed class NoteCollabEndpointTests(FunctionalTestWebAppFactory factory)
{
    private const string NotesEndpoint = "/api/v1/notes";

    [Fact]
    public async Task GivenOwner_WhenGeneratingOrRevokingShareLink_ThenSucceeds()
    {
        var owner = await CreateProvisionedClientAsync();
        await SetPlanTierAsync(owner, "Pro");
        var noteId = await CreateNoteAsync(owner, "Title", "Content");

        var generateResponse = await owner.PostAsync($"{NotesEndpoint}/{noteId}/share-link", content: null);
        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);
        var generateBody = await generateResponse.Content.ReadFromJsonAsync<ShareLinkResponse>();
        Assert.NotNull(generateBody);
        Assert.False(string.IsNullOrEmpty(generateBody.ShareToken));

        var revokeResponse = await owner.PostAsync($"{NotesEndpoint}/{noteId}/share-link/revoke", content: null);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        var revokeBody = await revokeResponse.Content.ReadFromJsonAsync<ShareLinkResponse>();
        Assert.NotNull(revokeBody);
        Assert.NotEqual(generateBody.ShareToken, revokeBody.ShareToken);
    }

    [Fact]
    public async Task GivenCollaborator_WhenGeneratingOrRevokingShareLinkOrRemovingCollaborator_ThenIsRejected()
    {
        // note-linking/notes-crud 既有的擁有權拒絕情境一律回 400(ErrorType.BadRequest), 不是 403——
        // 這裡沿用同一個慣例, 沒有另外引入 Forbidden/403, 避免同一種「不是擁有者」錯誤在 API 裡有兩種狀態碼。
        var owner = await CreateProvisionedClientAsync();
        await SetPlanTierAsync(owner, "Pro");
        var collaborator = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");
        var shareToken = await GenerateShareLinkAsync(owner, noteId);
        await JoinAsync(collaborator, shareToken);

        var generateResponse = await collaborator.PostAsync($"{NotesEndpoint}/{noteId}/share-link", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, generateResponse.StatusCode);

        var revokeResponse = await collaborator.PostAsync($"{NotesEndpoint}/{noteId}/share-link/revoke", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, revokeResponse.StatusCode);

        var removeResponse = await collaborator.DeleteAsync($"{NotesEndpoint}/{noteId}/collaborators/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.BadRequest, removeResponse.StatusCode);
    }

    [Fact]
    public async Task GivenAuthenticatedUser_WhenOpeningValidShareLink_ThenAddedAsCollaborator()
    {
        var owner = await CreateProvisionedClientAsync();
        await SetPlanTierAsync(owner, "Pro");
        var joiningUser = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");
        var shareToken = await GenerateShareLinkAsync(owner, noteId);

        var joinResponse = await joiningUser.PostAsync($"{NotesEndpoint}/share-link/{shareToken}/join", content: null);
        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);

        var getResponse = await joiningUser.GetAsync($"{NotesEndpoint}/{noteId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task GivenInvalidShareToken_WhenJoining_ThenIsRejected()
    {
        var joiningUser = await CreateProvisionedClientAsync();

        var joinResponse = await joiningUser.PostAsync($"{NotesEndpoint}/share-link/not-a-real-token/join", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, joinResponse.StatusCode);
    }

    [Fact]
    public async Task GivenCollaborator_WhenListingOrReadingOrUpdatingNote_ThenSucceeds_AndUnrelatedUserIsRejected()
    {
        var owner = await CreateProvisionedClientAsync();
        await SetPlanTierAsync(owner, "Pro");
        var collaborator = await CreateProvisionedClientAsync();
        var unrelatedUser = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");
        var shareToken = await GenerateShareLinkAsync(owner, noteId);
        await JoinAsync(collaborator, shareToken);

        var listResponse = await collaborator.GetAsync(NotesEndpoint);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ListNotesResponse>();
        Assert.NotNull(listBody);
        Assert.Contains(listBody.Notes, n => n.NoteId == noteId);

        var getResponse = await collaborator.GetAsync($"{NotesEndpoint}/{noteId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updateResponse = await collaborator.PutAsJsonAsync(
            $"{NotesEndpoint}/{noteId}", new { Title = "Updated by collaborator", Content = "New content" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var unrelatedListResponse = await unrelatedUser.GetAsync(NotesEndpoint);
        var unrelatedListBody = await unrelatedListResponse.Content.ReadFromJsonAsync<ListNotesResponse>();
        Assert.NotNull(unrelatedListBody);
        Assert.DoesNotContain(unrelatedListBody.Notes, n => n.NoteId == noteId);

        var unrelatedGetResponse = await unrelatedUser.GetAsync($"{NotesEndpoint}/{noteId}");
        Assert.Equal(HttpStatusCode.BadRequest, unrelatedGetResponse.StatusCode);

        var unrelatedUpdateResponse = await unrelatedUser.PutAsJsonAsync(
            $"{NotesEndpoint}/{noteId}", new { Title = "Hacked", Content = "Hacked" });
        Assert.Equal(HttpStatusCode.BadRequest, unrelatedUpdateResponse.StatusCode);
    }

    [Fact]
    public async Task GivenOwner_WhenRemovingCollaborator_ThenCollaboratorCanNoLongerAccessTheNote()
    {
        var owner = await CreateProvisionedClientAsync();
        await SetPlanTierAsync(owner, "Pro");
        var collaborator = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");
        var shareToken = await GenerateShareLinkAsync(owner, noteId);
        await JoinAsync(collaborator, shareToken);
        var collaboratorAppUserId = await GetAppUserIdAsync(collaborator);

        var removeResponse = await owner.DeleteAsync($"{NotesEndpoint}/{noteId}/collaborators/{collaboratorAppUserId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        var getResponse = await collaborator.GetAsync($"{NotesEndpoint}/{noteId}");
        Assert.Equal(HttpStatusCode.BadRequest, getResponse.StatusCode);
    }

    [Fact]
    public async Task GivenOwner_WhenGettingCollaborationSettings_ThenReturnsShareTokenAndCollaborators()
    {
        var owner = await CreateProvisionedClientAsync();
        await SetPlanTierAsync(owner, "Pro");
        var collaborator = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");
        var shareToken = await GenerateShareLinkAsync(owner, noteId);
        await JoinAsync(collaborator, shareToken);
        var collaboratorAppUserId = await GetAppUserIdAsync(collaborator);

        var response = await owner.GetAsync($"{NotesEndpoint}/{noteId}/collaboration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CollaborationResponse>();
        Assert.NotNull(body);
        Assert.Equal(shareToken, body.ShareToken);
        Assert.Equal([collaboratorAppUserId], body.CollaboratorAppUserIds);
    }

    [Fact]
    public async Task GivenCollaborator_WhenGettingCollaborationSettings_ThenIsRejected()
    {
        var owner = await CreateProvisionedClientAsync();
        await SetPlanTierAsync(owner, "Pro");
        var collaborator = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");
        var shareToken = await GenerateShareLinkAsync(owner, noteId);
        await JoinAsync(collaborator, shareToken);

        var response = await collaborator.GetAsync($"{NotesEndpoint}/{noteId}/collaboration");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GivenOwnerOrCollaborator_WhenSendingChatMessage_ThenMessageStoredAndReadable()
    {
        var owner = await CreateProvisionedClientAsync();
        await SetPlanTierAsync(owner, "ProMax");
        var collaborator = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");
        var shareToken = await GenerateShareLinkAsync(owner, noteId);
        await JoinAsync(collaborator, shareToken);

        var ownerSendResponse = await owner.PostAsJsonAsync(
            $"{NotesEndpoint}/{noteId}/chat/messages",
            new { Content = "Owner message" }
        );
        Assert.Equal(HttpStatusCode.OK, ownerSendResponse.StatusCode);

        var collaboratorSendResponse = await collaborator.PostAsJsonAsync(
            $"{NotesEndpoint}/{noteId}/chat/messages",
            new { Content = "Collaborator message" }
        );
        Assert.Equal(HttpStatusCode.OK, collaboratorSendResponse.StatusCode);

        var historyResponse = await owner.GetAsync($"{NotesEndpoint}/{noteId}/chat/messages");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<ChatHistoryResponse>();
        Assert.NotNull(history);
        Assert.Equal(
            ["Owner message", "Collaborator message"],
            history.Messages.Select(message => message.Content)
        );
    }

    [Fact]
    public async Task GivenNeitherOwnerNorCollaborator_WhenAccessingChatRoom_ThenRequestRejected()
    {
        var owner = await CreateProvisionedClientAsync();
        var unrelatedUser = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");

        var historyResponse = await unrelatedUser.GetAsync($"{NotesEndpoint}/{noteId}/chat/messages");
        Assert.Equal(HttpStatusCode.BadRequest, historyResponse.StatusCode);

        var sendResponse = await unrelatedUser.PostAsJsonAsync(
            $"{NotesEndpoint}/{noteId}/chat/messages",
            new { Content = "Unauthorized" }
        );
        Assert.Equal(HttpStatusCode.BadRequest, sendResponse.StatusCode);
    }

    [Fact]
    public async Task GivenMessageWithAiMention_WhenProcessed_ThenAiReplyEventuallyAppearsInChatRoom()
    {
        var owner = await CreateProvisionedClientAsync();
        await SetPlanTierAsync(owner, "ProMax");
        var noteId = await CreateNoteAsync(owner, "Title", "Content");

        var sendResponse = await owner.PostAsJsonAsync(
            $"{NotesEndpoint}/{noteId}/chat/messages",
            new { Content = "@AI 請摘要這篇筆記" }
        );
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);

        ChatHistoryResponse? history = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await Task.Delay(100);
            var historyResponse = await owner.GetAsync($"{NotesEndpoint}/{noteId}/chat/messages");
            history = await historyResponse.Content.ReadFromJsonAsync<ChatHistoryResponse>();
            if (history?.Messages.Any(message => message.IsAiReply) == true)
                break;
        }

        Assert.NotNull(history);
        var aiReply = Assert.Single(history.Messages, message => message.IsAiReply);
        Assert.Contains("AI 目前無法回應", aiReply.Content);
    }

    [Fact]
    public async Task GivenOwner_WhenGettingNoteHistory_ThenReturns200WithEmptyHistoryForANoteWithNoLiveEdits()
    {
        var owner = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");

        var response = await owner.GetAsync($"{NotesEndpoint}/{noteId}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<NoteHistoryResponse>();
        Assert.NotNull(body);
        Assert.Null(body.BaseSnapshot);
        Assert.Empty(body.SubsequentUpdates);
    }

    [Fact]
    public async Task GivenUnrelatedUser_WhenGettingNoteHistory_ThenIsRejected()
    {
        var owner = await CreateProvisionedClientAsync();
        var unrelatedUser = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");

        var response = await unrelatedUser.GetAsync($"{NotesEndpoint}/{noteId}/history");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> CreateProvisionedClientAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", TestTokens.CreateToken(Guid.NewGuid().ToString()));

        await client.PostAsync("/api/test/app-user", content: null);

        return client;
    }

    // subscription-billing 之後, 產生分享連結/使用聊天室分別要求擁有者訂閱等級 Pro 以上／ProMax;
    // 這裡跳過真的 PayPal 付款流程, 直接用測試專用 endpoint 把等級設好(見 Program.cs 的說明)。
    private static Task SetPlanTierAsync(HttpClient client, string planTier) =>
        client.PostAsJsonAsync("/api/test/app-user/plan-tier", new { PlanTier = planTier });

    private static async Task<Guid> CreateNoteAsync(HttpClient client, string title, string content)
    {
        var response = await client.PostAsJsonAsync(NotesEndpoint, new { Title = title, Content = content });
        var body = await response.Content.ReadFromJsonAsync<CreateNoteResponse>();

        return body!.NoteId;
    }

    private static async Task<string> GenerateShareLinkAsync(HttpClient owner, Guid noteId)
    {
        var response = await owner.PostAsync($"{NotesEndpoint}/{noteId}/share-link", content: null);
        var body = await response.Content.ReadFromJsonAsync<ShareLinkResponse>();

        return body!.ShareToken;
    }

    private static async Task<Guid> JoinAsync(HttpClient joiningUser, string shareToken)
    {
        var response = await joiningUser.PostAsync($"{NotesEndpoint}/share-link/{shareToken}/join", content: null);
        var body = await response.Content.ReadFromJsonAsync<JoinResponse>();

        return body!.NoteId;
    }

    private static async Task<Guid> GetAppUserIdAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/test/app-user", content: null);
        var body = await response.Content.ReadFromJsonAsync<AppUserResponse>();

        return body!.AppUserId;
    }

    private sealed record CreateNoteResponse(Guid NoteId);

    private sealed record ShareLinkResponse(string ShareToken);

    private sealed record JoinResponse(Guid NoteId);

    private sealed record AppUserResponse(Guid AppUserId);

    private sealed record NoteHistoryResponse(byte[]? BaseSnapshot, List<byte[]> SubsequentUpdates);

    private sealed record CollaborationResponse(string? ShareToken, List<Guid> CollaboratorAppUserIds);

    private sealed record ChatHistoryResponse(List<ChatMessageItemResponse> Messages);

    private sealed record ChatMessageItemResponse(
        Guid ChatMessageId,
        Guid? AuthorAppUserId,
        bool IsAiReply,
        string Content,
        DateTime CreatedAt
    );

    private sealed record NoteItem(Guid NoteId, string Title, string Content);

    private sealed record ListNotesResponse(List<NoteItem> Notes);
}
