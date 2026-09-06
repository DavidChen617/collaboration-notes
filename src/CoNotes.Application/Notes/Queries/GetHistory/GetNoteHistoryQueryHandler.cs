namespace CoNotes.Application.Notes.Queries.GetHistory;

internal sealed class GetNoteHistoryQueryHandler(
    IUserContext userContext,
    INoteRepository noteRepository,
    INoteEditHistoryStore editHistoryStore
) : IQueryHandler<GetNoteHistoryQuery, Result<GetNoteHistoryDto>>
{
    public async Task<Result<GetNoteHistoryDto>> HandleAsync(GetNoteHistoryQuery query, CancellationToken cancellationToken)
    {
        var note = await noteRepository.GetByIdAsync(query.NoteId, cancellationToken);

        if (note is null)
            return new Error("Note.GetHistory", "找不到筆記!", ErrorType.NotFound);

        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        if (!note.IsAccessibleBy(requestingAppUserId))
            return new Error("Note.GetHistory", "使用者沒有權限檢視這篇筆記的編輯歷史!", ErrorType.BadRequest);

        var history = await editHistoryStore.GetHistoryUpToAsync(query.NoteId, query.AtUtc, cancellationToken);

        return new GetNoteHistoryDto(history.BaseSnapshotPayload, history.SubsequentUpdatePayloads);
    }
}
