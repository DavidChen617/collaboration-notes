using Davish.Result;
using Davish.Sendr;

namespace Todo.Application.Todos.Queries.Get;

public sealed record GetTodoQuery(Guid TodoId) : IQuery<Result<GetTodoDto>>;

public sealed record GetTodoDto(
    Guid TodoId,
    string Title,
    string Description,
    bool IsCompleted,
    DateTime? CompletedOnUtc
);

