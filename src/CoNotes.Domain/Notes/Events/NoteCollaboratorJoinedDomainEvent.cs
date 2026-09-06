namespace CoNotes.Domain.Notes.Events;

public sealed record NoteCollaboratorJoinedDomainEvent(Guid NoteId, Guid AppUserId) : DomainEvent;
