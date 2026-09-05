namespace Todo.Application.Todos.Commands.Complete;

internal sealed class CompleteTodoCommandHandler(
    IUserContext userContext,
    ITodoRepository todoRepository,
    TimeProvider timeProvider
) : ICommandHandler<CompleteTodoCommand, Result>
{
    public async Task<Result> HandleAsync(
        CompleteTodoCommand command,
        CancellationToken cancellationToken
    )
    {
        var todo = await todoRepository.GetByIdAsync(command.TodoId, cancellationToken);

        if (todo is null)
            return new Error("Todo.Complete", "找不到 Todo!", ErrorType.NotFound);

        if (todo.UserId != userContext.UserId)
            return new Error("Todo.Complete", "使用者沒有權完成該 Todo!", ErrorType.BadRequest);

        var completeResult = todo.Complete(timeProvider.GetUtcNow().UtcDateTime);

        if (!completeResult.IsSuccess)
            return completeResult.Error;

        await todoRepository.UpdateAsync(todo, cancellationToken);

        return Result.Success();
    }
}

