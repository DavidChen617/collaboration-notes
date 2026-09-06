namespace CoNotes.Application.Notes.Commands.Update;

internal sealed class UpdateNoteCommandHandler(
    IUserContext userContext,
    INoteRepository noteRepository,
    TimeProvider timeProvider
) : ICommandHandler<UpdateNoteCommand, Result<UpdateNoteDto>>
{
    public async Task<Result<UpdateNoteDto>> HandleAsync(
        UpdateNoteCommand command,
        CancellationToken cancellationToken
    )
    {
        var note = await noteRepository.GetByIdAsync(command.NoteId, cancellationToken);

        if (note is null)
            return new Error("Note.Update", "找不到筆記!", ErrorType.NotFound);

        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        if (!note.IsAccessibleBy(requestingAppUserId))
            return new Error("Note.Update", "使用者沒有權限更新這篇筆記!", ErrorType.BadRequest);

        note.Update(command.Title, command.Content, timeProvider.GetUtcNow().UtcDateTime);

        var requestedTargetNoteIds = NoteLinkContentParser.ExtractLinkedNoteIds(command.Content);
        var ownedTargetNoteIds = await noteRepository.FindOwnedNoteIdsAsync(requestingAppUserId, requestedTargetNoteIds, cancellationToken);

        var resolveLinksResult = note.ResolveLinks(requestedTargetNoteIds, ownedTargetNoteIds);
        if (!resolveLinksResult.IsSuccess)
            return resolveLinksResult.Error;

        await noteRepository.UpdateAsync(note, cancellationToken);

        return new UpdateNoteDto(note.Id, note.Title, note.Content);
    }
}
