namespace CoNotes.Application.Notes.Commands.RemoveCollaborator;

internal sealed class RemoveCollaboratorCommandHandler(
    IUserContext userContext,
    INoteRepository noteRepository
) : ICommandHandler<RemoveCollaboratorCommand, Result>
{
    public async Task<Result> HandleAsync(RemoveCollaboratorCommand command, CancellationToken cancellationToken)
    {
        var note = await noteRepository.GetByIdAsync(command.NoteId, cancellationToken);

        if (note is null)
            return new Error("Note.RemoveCollaborator", "找不到筆記!", ErrorType.NotFound);

        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        if (!note.IsOwnedBy(requestingAppUserId))
            return new Error("Note.RemoveCollaborator", "使用者沒有權限移除共編者!", ErrorType.BadRequest);

        var result = note.RemoveCollaborator(command.CollaboratorAppUserId);

        if (!result.IsSuccess)
            return result.Error;

        await noteRepository.UpdateAsync(note, cancellationToken);

        return Result.Success();
    }
}
