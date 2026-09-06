namespace CoNotes.Domain.ChatMessages.Events;

public sealed record AiReplyRequestedDomainEvent(
    Guid NoteId,
    Guid TriggeringChatMessageId
) : DomainEvent;
