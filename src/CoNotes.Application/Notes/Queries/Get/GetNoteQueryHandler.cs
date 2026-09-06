namespace CoNotes.Application.Notes.Queries.Get;

internal sealed class GetNoteQueryHandler(
    IUserContext userContext,
    IDbConnectionFactory dbConnectionFactory
) : IQueryHandler<GetNoteQuery, Result<GetNoteDto>>
{
    public async Task<Result<GetNoteDto>> HandleAsync(GetNoteQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
            select
                id as NoteId,
                owner_app_user_id as OwnerAppUserId,
                title as Title,
                content as Content,
                created_at as CreatedOnUtc,
                updated_at as UpdatedOnUtc
            from notes
            where id = @NoteId
            """;

        using var connection = await dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var command = new CommandDefinition(sql, new { query.NoteId }, cancellationToken: cancellationToken);

        var note = await connection.QuerySingleOrDefaultAsync<NoteRow>(command);

        if (note is null)
            return new Error("Note.Get", "找不到筆記!", ErrorType.NotFound);

        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        if (note.OwnerAppUserId != requestingAppUserId)
            return new Error("Note.Get", "使用者沒有權檢視這篇筆記!", ErrorType.BadRequest);

        return new GetNoteDto(note.NoteId, note.Title, note.Content, note.CreatedOnUtc, note.UpdatedOnUtc);
    }

    private sealed record NoteRow(
        Guid NoteId,
        Guid OwnerAppUserId,
        string Title,
        string Content,
        DateTime CreatedOnUtc,
        DateTime UpdatedOnUtc
    );
}
