namespace CoNotes.Application.Notes.Commands.RemoveCollaborator;

public sealed record RemoveCollaboratorCommand(Guid NoteId, Guid CollaboratorAppUserId) : ICommand<Result>;
