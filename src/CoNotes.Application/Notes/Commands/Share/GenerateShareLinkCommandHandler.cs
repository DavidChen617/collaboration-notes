namespace CoNotes.Application.Notes.Commands.Share;

internal sealed class GenerateShareLinkCommandHandler(
    IUserContext userContext,
    INoteRepository noteRepository
) : ICommandHandler<GenerateShareLinkCommand, Result<GenerateShareLinkDto>>
{
    public async Task<Result<GenerateShareLinkDto>> HandleAsync(
        GenerateShareLinkCommand command,
        CancellationToken cancellationToken
    )
    {
        var note = await noteRepository.GetByIdAsync(command.NoteId, cancellationToken);

        if (note is null)
            return new Error("Note.GenerateShareLink", "找不到筆記!", ErrorType.NotFound);

        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        var result = note.GenerateShareLink(requestingAppUserId);

        if (!result.IsSuccess)
            return result.Error;

        await noteRepository.UpdateAsync(note, cancellationToken);

        return new GenerateShareLinkDto(result.Value);
    }
}
