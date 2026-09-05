using Todo.Application.Todos.Commands.Create;

namespace Todo.Api.Endpoints.v1.Todos;

internal sealed class CreateTodoEndpoint : IEndpoint<TodoGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/", HandleAsync)
            .WithName("CreateTodo")
            .WithSummary("建立待辦事項")
            .WithDescription("建立待辦事項, 請求體帶 Title, Description")
            .Produces<CreateTodoDto>();
    }

    private static async Task<IResult> HandleAsync(CreateTodoRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new CreateTodoCommand(request.Title, request.Description), ct);

        return result.ToCreated("GetById", v => new { v.TodoId });
    }
}

public sealed record CreateTodoRequest(string Title, string Description);

