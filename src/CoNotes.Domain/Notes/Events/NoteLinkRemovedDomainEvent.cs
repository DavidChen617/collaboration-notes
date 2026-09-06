namespace CoNotes.Domain.Notes.Events;

public sealed record NoteLinkRemovedDomainEvent(Guid SourceNoteId, Guid TargetNoteId) : DomainEvent;
