using Todo.Application.Todos.Queries.List;

namespace Todo.Api.Endpoints.v1.Todos;

internal sealed class ListTodosEndpoint : IEndpoint<TodoGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/", HandleAsync)
            .WithName("ListTodos")
            .WithSummary("列出待辦事項")
            .WithDescription("列出目前使用者所有未刪除的待辦事項")
            .Produces<ListTodosDto>();
    }

    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new ListTodosQuery(), ct);

        return result.ToOk();
    }
}

