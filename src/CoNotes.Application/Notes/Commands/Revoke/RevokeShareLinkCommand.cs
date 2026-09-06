namespace CoNotes.Application.Notes.Commands.Revoke;

public sealed record RevokeShareLinkCommand(Guid NoteId) : ICommand<Result<RevokeShareLinkDto>>;

public sealed record RevokeShareLinkDto(string ShareToken);
