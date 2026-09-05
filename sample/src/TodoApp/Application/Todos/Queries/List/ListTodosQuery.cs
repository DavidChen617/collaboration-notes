using Davish.Result;
using Davish.Sendr;

namespace Todo.Application.Todos.Queries.List;

public sealed record ListTodosQuery : IQuery<Result<ListTodosDto>>;

public sealed record ListTodosDto(List<TodoItem> Todos);

public sealed record TodoItem(
    Guid TodoId,
    string Title,
    string Description,
    bool IsCompleted,
    DateTime? CompletedOnUtc
);

