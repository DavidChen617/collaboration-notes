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

        if (!note.IsOwnedBy(requestingAppUserId))
            return new Error("Note.RevokeShareLink", "使用者沒有權限撤銷這篇筆記的分享連結!", ErrorType.BadRequest);

        note.RevokeShareLink();

        var newShareToken = new ShareLinkToken(Guid.NewGuid().ToString());
        note.SetShareLink(newShareToken);

        await noteRepository.UpdateAsync(note, cancellationToken);

        return new RevokeShareLinkDto(newShareToken.Value);
    }
}
