namespace Todo.Application.Todos.Commands.Delete;

internal sealed class DeleteTodoCommandHandler(
    IUserContext userContext,
    ITodoRepository todoRepository,
    TimeProvider timeProvider
) : ICommandHandler<DeleteTodoCommand, Result>
{
    public async Task<Result> HandleAsync(
        DeleteTodoCommand command,
        CancellationToken cancellationToken
    )
    {
        var todo = await todoRepository.GetByIdAsync(command.TodoId, cancellationToken);

        if (todo is null)
            return new Error("Todo.Delete", "找不到 Todo!", ErrorType.NotFound);

        if (todo.UserId != userContext.UserId)
            return new Error("Todo.Delete", "使用者沒有權刪除該 Todo!", ErrorType.BadRequest);

        var deleteResult = todo.Delete(timeProvider.GetUtcNow().UtcDateTime);

        if (!deleteResult.IsSuccess)
            return deleteResult.Error;

        await todoRepository.DeleteAsync(todo, cancellationToken);

        return Result.Success();
    }
}

