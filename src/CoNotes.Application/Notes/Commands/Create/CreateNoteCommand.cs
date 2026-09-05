namespace CoNotes.Application.Notes.Commands.Create;

public sealed record CreateNoteCommand(string Title, string Content) : ICommand<Result<CreateNoteDto>>;

public sealed record CreateNoteDto(Guid NoteId);
