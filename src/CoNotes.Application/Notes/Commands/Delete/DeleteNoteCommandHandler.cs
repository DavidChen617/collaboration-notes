namespace CoNotes.Application.Notes.Commands.Delete;

internal sealed class DeleteNoteCommandHandler(
    IUserContext userContext,
    INoteRepository noteRepository
) : ICommandHandler<DeleteNoteCommand, Result>
{
    public async Task<Result> HandleAsync(DeleteNoteCommand command, CancellationToken cancellationToken)
    {
        var note = await noteRepository.GetByIdAsync(command.NoteId, cancellationToken);

        if (note is null)
            return new Error("Note.Delete", "找不到筆記!", ErrorType.NotFound);

        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        if (!note.IsOwnedBy(requestingAppUserId))
            return new Error("Note.Delete", "使用者沒有權限刪除這篇筆記!", ErrorType.BadRequest);

        note.Delete();

        await noteRepository.DeleteAsync(note, cancellationToken);

        return Result.Success();
    }
}
