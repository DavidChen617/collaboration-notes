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

    private sealed record NoteItem(Guid NoteId, string Title, string Content);

    private sealed record ListNotesResponse(List<NoteItem> Notes);
}
