namespace Todo.Api.Endpoints.v1.Todos;

internal sealed class TodoGroupEndpoint : IGroupEndpoint<ApiVersionEndpoint>
{
    public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
    {
        var todos = endpoints
                    .MapGroup("todos")
                    .WithTags("todos")
                    .HasApiVersion(1.0)
                    .RequireAuthorization();

        return todos;
    }
}

