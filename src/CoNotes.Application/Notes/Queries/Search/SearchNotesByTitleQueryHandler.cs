namespace CoNotes.Application.Notes.Queries.Search;

internal sealed class SearchNotesByTitleQueryHandler(
    IUserContext userContext,
    IDbConnectionFactory dbConnectionFactory
) : IQueryHandler<SearchNotesByTitleQuery, Result<SearchNotesByTitleDto>>
{
    public async Task<Result<SearchNotesByTitleDto>> HandleAsync(
        SearchNotesByTitleQuery query,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
            select
                id as NoteId,
                title as Title
            from notes
            where owner_app_user_id = @OwnerAppUserId
              and title ilike @Pattern
            order by title
            """;

        var ownerAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        using var connection = await dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { OwnerAppUserId = ownerAppUserId, Pattern = $"%{query.Keyword}%" },
            cancellationToken: cancellationToken);

        var notes = await connection.QueryAsync<NoteSearchResult>(command);

        return new SearchNotesByTitleDto([.. notes]);
    }
}
