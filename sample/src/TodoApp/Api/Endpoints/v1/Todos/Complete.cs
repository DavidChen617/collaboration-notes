using Microsoft.AspNetCore.Mvc;
using Todo.Application.Todos.Commands.Complete;

namespace Todo.Api.Endpoints.v1.Todos;

internal sealed class CompleteTodoEndpoint : IEndpoint<TodoGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPatch("/{todoId}", HandleAsync)
            .WithName("CompleteTodo")
            .WithSummary("完成 Todo")
            .WithDescription("完成 Todo, 如果使用者無擁有該 Todo, 則會完成失敗!")
            .ProduceProblem(StatusCodes.Status404NotFound, "找不到 Todo")
            .ProduceProblem(StatusCodes.Status400BadRequest, "使用者沒有完成的權限!");
    }

    private static async Task<IResult> HandleAsync([FromRoute] Guid todoId, ISender sender, CancellationToken ct)
    {
        var result = await sender.SendAsync(new CompleteTodoCommand(todoId), ct);

        return result.ToNoContent();
    }
}

