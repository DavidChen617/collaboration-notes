namespace CoNotes.Application.Notes.Commands.Revoke;

internal sealed class RevokeShareLinkCommandHandler(
    IUserContext userContext,
    INoteRepository noteRepository
) : ICommandHandler<RevokeShareLinkCommand, Result<RevokeShareLinkDto>>
{
    public async Task<Result<RevokeShareLinkDto>> HandleAsync(
        RevokeShareLinkCommand command,
        CancellationToken cancellationToken
    )
    {
        var note = await noteRepository.GetByIdAsync(command.NoteId, cancellationToken);

        if (note is null)
            return new Error("Note.RevokeShareLink", "找不到筆記!", ErrorType.NotFound);

        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        var revokeResult = note.RevokeShareLink(requestingAppUserId);

        if (!revokeResult.IsSuccess)
            return revokeResult.Error;

        var newShareToken = new ShareLinkToken(Guid.NewGuid().ToString());
        var setResult = note.SetShareLink(requestingAppUserId, newShareToken);

        if (!setResult.IsSuccess)
            return setResult.Error;

        await noteRepository.UpdateAsync(note, cancellationToken);

        return new RevokeShareLinkDto(newShareToken.Value);
    }
}
