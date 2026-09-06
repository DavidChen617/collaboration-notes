using System.Data;

namespace CoNotes.Application.Notes.Queries.Get;

internal sealed class GetNoteQueryHandler(
    IUserContext userContext,
    IDbConnectionFactory dbConnectionFactory
) : IQueryHandler<GetNoteQuery, Result<GetNoteDto>>
{
    public async Task<Result<GetNoteDto>> HandleAsync(GetNoteQuery query, CancellationToken cancellationToken)
    {
        var param = new { query.NoteId };

        var sql = $"""
            select
                id as {nameof(NoteRow.NoteId)},
                owner_app_user_id as {nameof(NoteRow.OwnerAppUserId)},
                title as {nameof(NoteRow.Title)},
                content as {nameof(NoteRow.Content)},
                created_at as {nameof(NoteRow.CreatedOnUtc)},
                updated_at as {nameof(NoteRow.UpdatedOnUtc)}
            from notes
            where id = @{nameof(param.NoteId)}
            """;

        using var connection = await dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var command = new CommandDefinition(sql, param, cancellationToken: cancellationToken);

        var note = await connection.QuerySingleOrDefaultAsync<NoteRow>(command);

        if (note is null)
            return new Error("Note.Get", "找不到筆記!", ErrorType.NotFound);

        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        if (note.OwnerAppUserId != requestingAppUserId && !await IsCollaboratorAsync(connection, note.NoteId, requestingAppUserId, cancellationToken))
            return new Error("Note.Get", "使用者沒有權檢視這篇筆記!", ErrorType.BadRequest);

        return new GetNoteDto(note.NoteId, note.Title, note.Content, note.CreatedOnUtc, note.UpdatedOnUtc);
    }

    private static async Task<bool> IsCollaboratorAsync(
        IDbConnection connection,
        Guid noteId,
        Guid appUserId,
        CancellationToken cancellationToken
    )
    {
        var param = new { NoteId = noteId, AppUserId = appUserId };

        var sql = $"""
            select exists (
                select 1 from note_collaborators
                where note_id = @{nameof(param.NoteId)} and app_user_id = @{nameof(param.AppUserId)}
            )
            """;

        var command = new CommandDefinition(sql, param, cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(command);
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
