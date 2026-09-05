namespace CoNotes.Application.Notes.Commands.Update;

public sealed record UpdateNoteCommand(Guid NoteId, string Title, string Content) : ICommand<Result<UpdateNoteDto>>;

public sealed record UpdateNoteDto(Guid NoteId, string Title, string Content);
