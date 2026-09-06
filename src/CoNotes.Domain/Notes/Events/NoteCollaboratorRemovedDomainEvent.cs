namespace CoNotes.Domain.Notes.Events;

public sealed record NoteCollaboratorRemovedDomainEvent(Guid NoteId, Guid AppUserId) : DomainEvent;
