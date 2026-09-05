using System.Net;
using System.Net.Http.Json;
using CoNotes.FunctionalTests;

namespace FunctionalTests;

[Collection(nameof(ApiCollection))]
public sealed class NoteEndpointTests(FunctionalTestWebAppFactory factory)
{
    private const string NotesEndpoint = "/api/v1/notes";

    [Fact]
    public async Task GivenAuthenticatedUser_WhenCreatingANote_ThenReturns201WithTheNewNoteId()
    {
        var client = await CreateProvisionedClientAsync();

        var response = await client.PostAsJsonAsync(NotesEndpoint, new { Title = "Title", Content = "Content" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateNoteResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.NoteId);
    }

    [Fact]
    public async Task GivenNoToken_WhenCreatingANote_ThenReturns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(NotesEndpoint, new { Title = "Title", Content = "Content" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GivenFirstTimeUser_WhenCallingNotesWithoutEverProvisioningExplicitly_ThenAppUserIsAutoCreatedAndTheCallSucceeds()
    {
        // Regression test: a real user's very first call after login used to be a random Notes
        // endpoint, not the throwaway /api/test/app-user endpoint - AppUser provisioning must
        // happen for any protected endpoint, not just that one. Deliberately does NOT call
        // /api/test/app-user first (unlike CreateProvisionedClientAsync), to prove the
        // OnTokenValidated hook provisions the AppUser on its own.
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", TestTokens.CreateToken(Guid.NewGuid().ToString()));

        var response = await client.GetAsync(NotesEndpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GivenOwnerOnly_WhenListingNotes_ThenOnlyTheCallersOwnNotesAreReturned()
    {
        var owner = await CreateProvisionedClientAsync();
        var otherUser = await CreateProvisionedClientAsync();

        await owner.PostAsJsonAsync(NotesEndpoint, new { Title = "Owner's note", Content = "Content" });
        await otherUser.PostAsJsonAsync(NotesEndpoint, new { Title = "Other user's note", Content = "Content" });

        var response = await owner.GetAsync(NotesEndpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ListNotesResponse>();
        Assert.NotNull(body);
        Assert.Contains(body.Notes, n => n.Title == "Owner's note");
        Assert.DoesNotContain(body.Notes, n => n.Title == "Other user's note");
    }

    [Fact]
    public async Task GivenOwner_WhenGettingTheirNote_ThenReturnsItsContent()
    {
        var owner = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");

        var response = await owner.GetAsync($"{NotesEndpoint}/{noteId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetNoteResponse>();
        Assert.NotNull(body);
        Assert.Equal("Title", body.Title);
    }

    [Fact]
    public async Task GivenNonOwner_WhenGettingSomeoneElsesNote_ThenIsRejected()
    {
        var owner = await CreateProvisionedClientAsync();
        var otherUser = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");

        var response = await otherUser.GetAsync($"{NotesEndpoint}/{noteId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GivenOwner_WhenUpdatingTheirNote_ThenAppliesTheChange()
    {
        var owner = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Old title", "Old content");

        var response = await owner.PutAsJsonAsync($"{NotesEndpoint}/{noteId}", new { Title = "New title", Content = "New content" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetNoteResponse>();
        Assert.NotNull(body);
        Assert.Equal("New title", body.Title);

        var getResponse = await owner.GetAsync($"{NotesEndpoint}/{noteId}");
        var getBody = await getResponse.Content.ReadFromJsonAsync<GetNoteResponse>();
        Assert.Equal("New content", getBody?.Content);
    }

    [Fact]
    public async Task GivenNonOwner_WhenUpdatingSomeoneElsesNote_ThenIsRejectedAndLeavesItUnchanged()
    {
        var owner = await CreateProvisionedClientAsync();
        var otherUser = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");

        var response = await otherUser.PutAsJsonAsync($"{NotesEndpoint}/{noteId}", new { Title = "Hacked", Content = "Hacked" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var getResponse = await owner.GetAsync($"{NotesEndpoint}/{noteId}");
        var getBody = await getResponse.Content.ReadFromJsonAsync<GetNoteResponse>();
        Assert.Equal("Title", getBody?.Title);
    }

    [Fact]
    public async Task GivenOwner_WhenDeletingTheirNote_ThenItCanNoLongerBeRead()
    {
        var owner = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");

        var deleteResponse = await owner.DeleteAsync($"{NotesEndpoint}/{noteId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await owner.GetAsync($"{NotesEndpoint}/{noteId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GivenNonOwner_WhenDeletingSomeoneElsesNote_ThenIsRejectedAndItStillExists()
    {
        var owner = await CreateProvisionedClientAsync();
        var otherUser = await CreateProvisionedClientAsync();
        var noteId = await CreateNoteAsync(owner, "Title", "Content");

        var deleteResponse = await otherUser.DeleteAsync($"{NotesEndpoint}/{noteId}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        var getResponse = await owner.GetAsync($"{NotesEndpoint}/{noteId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
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

    private sealed record CreateNoteResponse(Guid NoteId);

    private sealed record NoteItem(Guid NoteId, string Title, string Content);

    private sealed record ListNotesResponse(List<NoteItem> Notes);

    private sealed record GetNoteResponse(Guid NoteId, string Title, string Content);
}
