using Microsoft.AspNetCore.Mvc;
using Todo.Application.Todos.Commands.Delete;

namespace Todo.Api.Endpoints.v1.Todos;

internal sealed class DeleteTodoEndpoint : IEndpoint<TodoGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapDelete("/{todoId}", HandleAsync)
            .WithName("DeleteTodo")
            .WithSummary("刪除 Todo")
            .WithDescription("刪除 Todo, 如果使用者無擁有該 Todo, 則會刪除失敗!")
            .ProduceProblem(StatusCodes.Status404NotFound, "找不到 Todo")
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有刪除的權限!");
    }

    private static async Task<IResult> HandleAsync([FromRoute] Guid todoId, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new DeleteTodoCommand(todoId), ct);
        return result.ToNoContent();
    }
}

