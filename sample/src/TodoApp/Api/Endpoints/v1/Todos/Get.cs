using Microsoft.AspNetCore.Mvc;
using Todo.Application.Todos.Queries.Get;

namespace Todo.Api.Endpoints.v1.Todos;

internal sealed class GetTodoEndpoint : IEndpoint<TodoGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/{todoId}", HandleAsync)
            .WithName("GetTodo")
            .WithSummary("取得單筆 Todo")
            .WithDescription("依 todoId 取得單筆 Todo, 如果使用者無擁有該 Todo, 則會取得失敗!")
            .Produces<GetTodoDto>()
            .ProduceProblem(StatusCodes.Status404NotFound, "找不到 Todo")
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有檢視的權限!");
    }

    private static async Task<IResult> HandleAsync([FromRoute] Guid todoId, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new GetTodoQuery(todoId), ct);

        return result.ToOk();
    }
}

