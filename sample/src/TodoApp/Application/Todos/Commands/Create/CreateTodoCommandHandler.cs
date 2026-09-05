using TodoAggregate = Todo.Domain.Todos.Todo;

namespace Todo.Application.Todos.Commands.Create;

internal sealed class CreateTodoCommandHandler(
    IUserContext userContext,
    ITodoRepository todoRepository
) : ICommandHandler<CreateTodoCommand, Result<CreateTodoDto>>
{
    public async Task<Result<CreateTodoDto>> HandleAsync(
        CreateTodoCommand command,
        CancellationToken cancellationToken
    )
    {
        var todo = TodoAggregate.Create(userContext.UserId, command.Title, command.Description);

        await todoRepository.AddAsync(todo, cancellationToken);

        return new CreateTodoDto(todo.Id);
    }
}

