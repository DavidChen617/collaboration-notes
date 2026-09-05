using Davish.Endpoints;

namespace Todo.Api.Endpoints;

internal sealed class ApiVersionEndpoint : IGroupEndpoint
{
    public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
    {
        var version = endpoints.NewVersionedApi();

        var group = version.MapGroup("api/v{version:apiVersion}");

        return group;
    }
}

