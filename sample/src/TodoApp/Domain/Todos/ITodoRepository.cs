namespace Todo.Domain.Todos;

public interface ITodoRepository
{
    Task<Todo?> GetByIdAsync(Guid todoId, CancellationToken ct);
    Task<Result> AddAsync(Todo todo, CancellationToken ct);
    Task<Result> AddRangeAsync(List<Todo> todo, CancellationToken ct);
    Task<Result> DeleteAsync(Todo todo, CancellationToken ct);
    Task<Result> DeleteRangeAsync(List<Todo> todos, CancellationToken ct);
    Task<Result> UpdateAsync(Todo todo, CancellationToken ct);
}

