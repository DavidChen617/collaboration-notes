using CoNotes.Application.Abstractions;
using CoNotes.Domain.Notes;

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

        var deleteResult = note.Delete(requestingAppUserId);

        if (!deleteResult.IsSuccess)
            return deleteResult.Error;

        await noteRepository.DeleteAsync(note, cancellationToken);

        return Result.Success();
    }
}
