namespace CoNotes.Domain.Notes.Events;

public sealed record NoteCreatedDomainEvent(Guid NoteId, Guid OwnerAppUserId) : DomainEvent;
