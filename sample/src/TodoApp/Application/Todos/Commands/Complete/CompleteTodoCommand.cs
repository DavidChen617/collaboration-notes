namespace Todo.Application.Todos.Commands.Complete;

public sealed record CompleteTodoCommand(Guid TodoId) : ICommand<Result>;

