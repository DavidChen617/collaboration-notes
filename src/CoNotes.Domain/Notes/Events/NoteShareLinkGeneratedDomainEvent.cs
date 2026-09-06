namespace CoNotes.Domain.Notes.Events;

public sealed record NoteShareLinkGeneratedDomainEvent(Guid NoteId, ShareLinkToken ShareToken) : DomainEvent;
