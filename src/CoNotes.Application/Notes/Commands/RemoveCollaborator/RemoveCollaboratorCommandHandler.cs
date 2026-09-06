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

        var result = note.RemoveCollaborator(requestingAppUserId, command.CollaboratorAppUserId);

        if (!result.IsSuccess)
            return result.Error;

        await noteRepository.UpdateAsync(note, cancellationToken);

        return Result.Success();
    }
}
