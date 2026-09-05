using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoNotes.FunctionalTests;
using Microsoft.IdentityModel.Tokens;

namespace FunctionalTests;

[Collection(nameof(ApiCollection))]
public sealed class AppUserEndpointTests(FunctionalTestWebAppFactory factory)
{
    private const string Endpoint = "/api/test/app-user";

    [Fact]
    public async Task GivenValidToken_WhenCallingProtectedEndpoint_ThenReturns200AndUpsertsAppUser()
    {
        var keycloakSub = Guid.NewGuid().ToString();
        var client = CreateAuthorizedClient(keycloakSub);

        var response = await client.PostAsync(Endpoint, content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UpsertAppUserResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.AppUserId);
    }

    [Fact]
    public async Task GivenNoToken_WhenCallingProtectedEndpoint_ThenReturns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(Endpoint, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GivenExpiredOrInvalidSignatureToken_WhenCallingProtectedEndpoint_ThenReturns401()
    {
        var expiredTokenClient = factory.CreateClient();
        expiredTokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.CreateToken(Guid.NewGuid().ToString(), expires: DateTime.UtcNow.AddMinutes(-5)));

        var expiredResponse = await expiredTokenClient.PostAsync(Endpoint, content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);

        var wrongKey = new SymmetricSecurityKey("a-completely-different-signing-key-32bytes+"u8.ToArray());
        var invalidSignatureClient = factory.CreateClient();
        invalidSignatureClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.CreateToken(Guid.NewGuid().ToString(), signingKey: wrongKey));

        var invalidSignatureResponse = await invalidSignatureClient.PostAsync(Endpoint, content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidSignatureResponse.StatusCode);
    }

    [Fact]
    public async Task GivenSameUserCallsTwice_WhenSecondRequestArrives_ThenNoDuplicateAppUserIsCreated()
    {
        var keycloakSub = Guid.NewGuid().ToString();
        var client = CreateAuthorizedClient(keycloakSub);

        var firstResponse = await client.PostAsync(Endpoint, content: null);
        var secondResponse = await client.PostAsync(Endpoint, content: null);

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<UpsertAppUserResponse>();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<UpsertAppUserResponse>();

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(firstBody.AppUserId, secondBody.AppUserId);
    }

    private HttpClient CreateAuthorizedClient(string keycloakSub)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.CreateToken(keycloakSub));

        return client;
    }

    private sealed record UpsertAppUserResponse(Guid AppUserId);
}
