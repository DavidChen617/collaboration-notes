using System.Net;
using CoNotes.FunctionalTests;

namespace FunctionalTests;

[Collection(nameof(ApiCollection))]
public sealed class CorsTests(FunctionalTestWebAppFactory factory)
{
    [Fact]
    public async Task GivenFrontendOrigin_WhenPreflightingSignalRNegotiate_ThenCredentialsAreAllowed()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/notes/negotiate");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }
}
