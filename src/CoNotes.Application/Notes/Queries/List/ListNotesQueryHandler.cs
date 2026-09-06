namespace CoNotes.Application.Notes.Queries.List;

internal sealed class ListNotesQueryHandler(
    IUserContext userContext,
    IDbConnectionFactory dbConnectionFactory
) : IQueryHandler<ListNotesQuery, Result<ListNotesDto>>
{
    public async Task<Result<ListNotesDto>> HandleAsync(ListNotesQuery query, CancellationToken cancellationToken)
    {
        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);
        var param = new { RequestingAppUserId = requestingAppUserId };

        var sql = $"""
            select
                id as NoteId,
                title as Title,
                content as Content,
                created_at as CreatedOnUtc,
                updated_at as UpdatedOnUtc
            from notes
            where owner_app_user_id = @{nameof(param.RequestingAppUserId)}
               or exists (
                   select 1 from note_collaborators
                   where note_id = notes.id and app_user_id = @{nameof(param.RequestingAppUserId)}
               )
            """;

        using var connection = await dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var command = new CommandDefinition(sql, param, cancellationToken: cancellationToken);

        var notes = await connection.QueryAsync<NoteItem>(command);

        return new ListNotesDto([.. notes]);
    }
}
