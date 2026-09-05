namespace CoNotes.Domain.Notes.Events;

public sealed record NoteDeletedDomainEvent(Guid NoteId) : DomainEvent;
