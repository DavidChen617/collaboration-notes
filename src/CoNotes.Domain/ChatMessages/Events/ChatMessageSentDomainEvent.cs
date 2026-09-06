namespace CoNotes.Domain.ChatMessages.Events;

public sealed record ChatMessageSentDomainEvent(
    Guid ChatMessageId,
    Guid NoteId,
    Guid AuthorAppUserId,
    bool ContainsAiMention
) : DomainEvent;
