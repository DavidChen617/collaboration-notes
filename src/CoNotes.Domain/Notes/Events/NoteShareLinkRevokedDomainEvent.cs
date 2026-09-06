namespace CoNotes.Domain.Notes.Events;

public sealed record NoteShareLinkRevokedDomainEvent(Guid NoteId) : DomainEvent;
