namespace Todo.Application.Todos.Commands.Create;

public sealed record CreateTodoCommand(string Title, string Description)
    : ICommand<Result<CreateTodoDto>>;

public sealed record CreateTodoDto(Guid TodoId);

