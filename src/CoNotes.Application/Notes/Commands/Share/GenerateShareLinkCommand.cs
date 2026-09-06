namespace CoNotes.Application.Notes.Commands.Share;

public sealed record GenerateShareLinkCommand(Guid NoteId) : ICommand<Result<GenerateShareLinkDto>>;

public sealed record GenerateShareLinkDto(Guid ShareToken);
