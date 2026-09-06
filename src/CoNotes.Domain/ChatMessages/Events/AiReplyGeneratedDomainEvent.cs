namespace CoNotes.Domain.ChatMessages.Events;

public sealed record AiReplyGeneratedDomainEvent(
    Guid ChatMessageId,
    Guid NoteId,
    string ProviderUsed
) : DomainEvent;
