namespace CoNotes.Application.Notes.Commands.Delete;

public sealed record DeleteNoteCommand(Guid NoteId) : ICommand<Result>;
