namespace CoNotes.Application.Notes.Queries.GetCollaboration;

internal sealed class GetNoteCollaborationQueryHandler(
    IUserContext userContext,
    INoteRepository noteRepository
) : IQueryHandler<GetNoteCollaborationQuery, Result<GetNoteCollaborationDto>>
{
    public async Task<Result<GetNoteCollaborationDto>> HandleAsync(
        GetNoteCollaborationQuery query,
        CancellationToken cancellationToken
    )
    {
        var note = await noteRepository.GetByIdAsync(query.NoteId, cancellationToken);

        if (note is null)
            return new Error("Note.GetCollaboration", "找不到筆記!", ErrorType.NotFound);

        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        if (!note.IsOwnedBy(requestingAppUserId))
            return new Error("Note.GetCollaboration", "使用者沒有權限管理這篇筆記的共編設定!", ErrorType.BadRequest);

        return new GetNoteCollaborationDto(note.ShareToken?.Value, note.CollaboratorAppUserIds);
    }
}
