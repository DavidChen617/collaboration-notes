using Dapper;
using Davish.Result;
using Davish.Sendr;
using Todo.Application.Absctractions;

namespace Todo.Application.Todos.Queries.List;

internal sealed class ListTodosQueryHandler(
    IUserContext userContext,
    IDbConnectionFactory dbConnectionFactory
) : IQueryHandler<ListTodosQuery, Result<ListTodosDto>>
{
    public async Task<Result<ListTodosDto>> HandleAsync(
        ListTodosQuery query,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
            SELECT
                id AS TodoId,
                title AS Title,
                description AS Description,
                completed_on_utc IS NOT NULL AS IsCompleted,
                completed_on_utc AS CompletedOnUtc
            FROM todos
            WHERE user_id = @UserId AND deleted_on_utc IS NULL
            """;

        using var connection = await dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { userContext.UserId },
            cancellationToken: cancellationToken
        );

        var todos = await connection.QueryAsync<TodoItem>(command);

        return new ListTodosDto([.. todos]);
    }
}

