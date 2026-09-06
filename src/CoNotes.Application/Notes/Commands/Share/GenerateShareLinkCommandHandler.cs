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

        if (!note.IsOwnedBy(requestingAppUserId))
            return new Error("Note.GenerateShareLink", "使用者沒有權限產生這篇筆記的分享連結!", ErrorType.BadRequest);

        var shareToken = new ShareLinkToken(Guid.NewGuid().ToString());

        note.SetShareLink(shareToken);

        await noteRepository.UpdateAsync(note, cancellationToken);

        return new GenerateShareLinkDto(shareToken.Value);
    }
}
