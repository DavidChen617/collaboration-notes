namespace CoNotes.Application.Notes.Commands.Join;

internal sealed class JoinNoteViaShareLinkCommandHandler(
    IUserContext userContext,
    INoteRepository noteRepository
) : ICommandHandler<JoinNoteViaShareLinkCommand, Result<JoinNoteViaShareLinkDto>>
{
    public async Task<Result<JoinNoteViaShareLinkDto>> HandleAsync(
        JoinNoteViaShareLinkCommand command,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(command.ShareToken))
            return new Error("Note.JoinViaShareLink", "分享連結無效或已失效!", ErrorType.BadRequest);

        var shareToken = new ShareLinkToken(command.ShareToken);

        var note = await noteRepository.GetByShareTokenAsync(shareToken, cancellationToken);

        if (note is null)
            return new Error("Note.JoinViaShareLink", "分享連結無效或已失效!", ErrorType.BadRequest);

        var joiningAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        var result = note.JoinViaShareLink(shareToken, joiningAppUserId);

        if (!result.IsSuccess)
            return result.Error;

        await noteRepository.UpdateAsync(note, cancellationToken);

        return new JoinNoteViaShareLinkDto(note.Id);
    }
}
