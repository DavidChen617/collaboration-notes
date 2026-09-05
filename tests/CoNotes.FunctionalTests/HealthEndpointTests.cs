using System.Net;

namespace FunctionalTests;

[Collection(nameof(ApiCollection))]
public sealed class HealthEndpointTests(FunctionalTestWebAppFactory factory)
{
    [Fact]
    public async Task GivenNoToken_WhenCallingHealthCheckEndpoint_ThenReturns200()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
