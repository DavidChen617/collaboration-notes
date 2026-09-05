using CoNotes.Application.Abstractions;
using Dapper;

namespace CoNotes.Application.Notes.Queries.List;

internal sealed class ListNotesQueryHandler(
    IUserContext userContext,
    IDbConnectionFactory dbConnectionFactory
) : IQueryHandler<ListNotesQuery, Result<ListNotesDto>>
{
    public async Task<Result<ListNotesDto>> HandleAsync(ListNotesQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
            select
                id as NoteId,
                title as Title,
                content as Content,
                created_at as CreatedOnUtc,
                updated_at as UpdatedOnUtc
            from notes
            where owner_app_user_id = @OwnerAppUserId
            """;

        var ownerAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        using var connection = await dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var command = new CommandDefinition(sql, new { OwnerAppUserId = ownerAppUserId }, cancellationToken: cancellationToken);

        var notes = await connection.QueryAsync<NoteItem>(command);

        return new ListNotesDto([.. notes]);
    }
}
