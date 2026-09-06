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

        var updateResult = note.Update(requestingAppUserId, command.Title, command.Content, timeProvider.GetUtcNow().UtcDateTime);

        if (!updateResult.IsSuccess)
            return updateResult.Error;

        var requestedTargetNoteIds = NoteLinkContentParser.ExtractLinkedNoteIds(command.Content);
        var ownedTargetNoteIds = await noteRepository.FindOwnedNoteIdsAsync(requestingAppUserId, requestedTargetNoteIds, cancellationToken);

        var resolveLinksResult = note.ResolveLinks(requestedTargetNoteIds, ownedTargetNoteIds);
        if (!resolveLinksResult.IsSuccess)
            return resolveLinksResult.Error;

        await noteRepository.UpdateAsync(note, cancellationToken);

        return new UpdateNoteDto(note.Id, note.Title, note.Content);
    }
}
