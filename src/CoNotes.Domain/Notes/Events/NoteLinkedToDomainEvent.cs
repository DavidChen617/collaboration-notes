namespace CoNotes.Domain.Notes.Events;

public sealed record NoteLinkedToDomainEvent(Guid SourceNoteId, Guid TargetNoteId) : DomainEvent;
