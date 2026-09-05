namespace Todo.Application.Todos.Commands.Delete;

public sealed record DeleteTodoCommand(Guid TodoId) : ICommand<Result>;

