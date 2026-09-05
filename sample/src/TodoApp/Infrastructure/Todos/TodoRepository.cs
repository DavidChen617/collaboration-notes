using Todo.Domain.Todos;
using TodoAggregateRoot = Todo.Domain.Todos.Todo;

namespace Todo.Infrastructure.Todos;

internal sealed class TodoRepository(AppDbContext appDbContext) : ITodoRepository
{
    public async Task<Result> AddAsync(TodoAggregateRoot todo, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
                $"""
                insert into todos (id, user_id, title, description, completed_on_utc, deleted_on_utc)
                values ( @{nameof(todo.Id)}, 
                         @{nameof(todo.UserId)}, 
                         @{nameof(todo.Title)}, 
                         @{nameof(todo.Description)}, 
                         @{nameof(todo.CompletedOnUtc)}, 
                         @{nameof(todo.DeletedOnUtc)} );
                """,
                todo,
                cancellationToken: ct,
                transaction: appDbContext.Transaction);

        var conn = await appDbContext.GetDbConnectionAsync(ct);

        await conn.ExecuteAsync(cmd);

        appDbContext.TrackAggregateRoot(todo);

        return Result.Success();
    }

    public async Task<Result> AddRangeAsync(List<TodoAggregateRoot> todos, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
                $"""
                insert into todos (id, user_id, title, description, completed_on_utc, deleted_on_utc)
                values (@{nameof(TodoAggregateRoot.Id)},
                        @{nameof(TodoAggregateRoot.UserId)},
                        @{nameof(TodoAggregateRoot.Title)},
                        @{nameof(TodoAggregateRoot.Description)},
                        @{nameof(TodoAggregateRoot.CompletedOnUtc)},
                        @{nameof(TodoAggregateRoot.DeletedOnUtc)});
                """,
                todos,
                cancellationToken: ct,
                transaction: appDbContext.Transaction);

        var conn = await appDbContext.GetDbConnectionAsync(ct);

        await conn.ExecuteAsync(cmd);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(TodoAggregateRoot todo, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
                $"""
                update todos
                set deleted_on_utc = @{nameof(todo.DeletedOnUtc)}
                where id = @{nameof(todo.Id)};
                """,
                todo,
                cancellationToken: ct,
                transaction: appDbContext.Transaction);

        var conn = await appDbContext.GetDbConnectionAsync(ct);

        await conn.ExecuteAsync(cmd);

        appDbContext.TrackAggregateRoot(todo);

        return Result.Success();
    }

    public async Task<Result> DeleteRangeAsync(List<TodoAggregateRoot> todos, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
                $"""
                update todos
                set deleted_on_utc = @{nameof(TodoAggregateRoot.DeletedOnUtc)}
                where id = @{nameof(TodoAggregateRoot.Id)};
                """,
                todos,
                cancellationToken: ct,
                transaction: appDbContext.Transaction);

        var conn = await appDbContext.GetDbConnectionAsync(ct);

        await conn.ExecuteAsync(cmd);

        return Result.Success();
    }

    public async Task<TodoAggregateRoot?> GetByIdAsync(Guid todoId, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
                $"""
                select
                    id as {nameof(TodoRow.Id)},
                    user_id as {nameof(TodoRow.UserId)},
                    title as {nameof(TodoRow.Title)},
                    description as {nameof(TodoRow.Description)},
                    completed_on_utc as {nameof(TodoRow.CompletedOnUtc)},
                    deleted_on_utc as {nameof(TodoRow.DeletedOnUtc)}
                from todos
                where id = @TodoId;
                """,
                new { TodoId = todoId },
                cancellationToken: ct,
                transaction: appDbContext.Transaction);

        var conn = await appDbContext.GetDbConnectionAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<TodoRow>(cmd);

        return row is null
            ? null
            : TodoAggregateRoot.Rehydrate(
                row.Id,
                row.UserId,
                row.Title,
                row.Description,
                row.CompletedOnUtc,
                row.DeletedOnUtc
            );
    }

    public async Task<Result> UpdateAsync(TodoAggregateRoot todo, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
                $"""
                update todos
                set title = @{nameof(todo.Title)},
                    description = @{nameof(todo.Description)},
                    completed_on_utc = @{nameof(todo.CompletedOnUtc)},
                    deleted_on_utc = @{nameof(todo.DeletedOnUtc)}
                where id = @{nameof(todo.Id)};
                """,
                todo,
                cancellationToken: ct,
                transaction: appDbContext.Transaction);

        var conn = await appDbContext.GetDbConnectionAsync(ct);

        await conn.ExecuteAsync(cmd);

        appDbContext.TrackAggregateRoot(todo);

        return Result.Success();
    }

    private sealed record TodoRow(
        Guid Id,
        Guid UserId,
        string Title,
        string Description,
        DateTime? CompletedOnUtc,
        DateTime? DeletedOnUtc
    );
}

