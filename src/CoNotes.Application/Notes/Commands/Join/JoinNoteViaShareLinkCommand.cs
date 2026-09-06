namespace CoNotes.Application.Notes.Commands.Join;

public sealed record JoinNoteViaShareLinkCommand(Guid ShareToken) : ICommand<Result<JoinNoteViaShareLinkDto>>;

public sealed record JoinNoteViaShareLinkDto(Guid NoteId);
