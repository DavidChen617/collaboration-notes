namespace CoNotes.Domain.ChatMessages.Events;

public sealed record AiReplyFailedDomainEvent(
    Guid NoteId,
    Guid TriggeringChatMessageId
) : DomainEvent;
