using System.Net;
using BuildBlock;

namespace FunctionalTests;

[Collection(nameof(ApiCollection))]
public sealed class TodosEndpointTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task Listing_todos_without_a_token_returns_unauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/todos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

