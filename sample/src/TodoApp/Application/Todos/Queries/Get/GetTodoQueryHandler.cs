using Dapper;
using Davish.Result;
using Davish.Sendr;
using Todo.Application.Absctractions;

namespace Todo.Application.Todos.Queries.Get;

internal sealed class GetTodoQueryHandler(
    IUserContext userContext,
    IDbConnectionFactory dbConnectionFactory
) : IQueryHandler<GetTodoQuery, Result<GetTodoDto>>
{
    public async Task<Result<GetTodoDto>> HandleAsync(
        GetTodoQuery query,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
            SELECT
                id AS TodoId,
                title AS Title,
                description AS Description,
                user_id AS UserId,
                completed_on_utc IS NOT NULL AS IsCompleted,
                completed_on_utc AS CompletedOnUtc,
                deleted_on_utc AS DeletedOnUtc
            FROM todos
            WHERE id = @TodoId
            """;

        using var connection = await dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { query.TodoId },
            cancellationToken: cancellationToken
        );

        var todo = await connection.QuerySingleOrDefaultAsync<TodoRow>(command);

        if (todo is null || todo.DeletedOnUtc is not null)
            return new Error("Todo.Get", "找不到 Todo!", ErrorType.NotFound);

        if (todo.UserId != userContext.UserId)
            return new Error("Todo.Get", "使用者沒有權檢視該 Todo!", ErrorType.BadRequest);

        return new GetTodoDto(
            todo.TodoId,
            todo.Title,
            todo.Description,
            todo.IsCompleted,
            todo.CompletedOnUtc
        );
    }

    private sealed record TodoRow(
        Guid TodoId,
        string Title,
        string Description,
        Guid UserId,
        bool IsCompleted,
        DateTime? CompletedOnUtc,
        DateTime? DeletedOnUtc
    );
}

