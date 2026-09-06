using System.Text.RegularExpressions;
using CoNotes.Domain.ChatMessages.Events;

namespace CoNotes.Domain.ChatMessages;

public sealed partial class ChatMessage : AggregateRoot
{
    public Guid NoteId { get; private set; }
    public Guid? AuthorAppUserId { get; private set; }
    public bool IsAiReply { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private ChatMessage(
        Guid id,
        Guid noteId,
        Guid? authorAppUserId,
        bool isAiReply,
        string content,
        DateTime createdAt
    )
    {
        Id = id;
        NoteId = noteId;
        AuthorAppUserId = authorAppUserId;
        IsAiReply = isAiReply;
        Content = content;
        CreatedAt = createdAt;
    }

    public static ChatMessage Create(Guid noteId, Guid authorAppUserId, string content, DateTime nowUtc)
    {
        var message = new ChatMessage(
            Guid.CreateVersion7(),
            noteId,
            authorAppUserId,
            isAiReply: false,
            content,
            nowUtc
        );
        var containsAiMention = AiMentionRegex().IsMatch(content);

        message.RaiseDomainEvent(new ChatMessageSentDomainEvent(
            message.Id,
            noteId,
            authorAppUserId,
            containsAiMention
        ));

        if (containsAiMention)
            message.RaiseDomainEvent(new AiReplyRequestedDomainEvent(noteId, message.Id));

        return message;
    }

    public static ChatMessage CreateAiReply(
        Guid noteId,
        string content,
        string providerUsed,
        DateTime nowUtc
    )
    {
        var message = new ChatMessage(
            Guid.CreateVersion7(),
            noteId,
            authorAppUserId: null,
            isAiReply: true,
            content,
            nowUtc
        );

        message.RaiseDomainEvent(new AiReplyGeneratedDomainEvent(message.Id, noteId, providerUsed));

        return message;
    }

    public static ChatMessage CreateAiFailure(
        Guid noteId,
        Guid triggeringChatMessageId,
        string content,
        DateTime nowUtc
    )
    {
        var message = new ChatMessage(
            Guid.CreateVersion7(),
            noteId,
            authorAppUserId: null,
            isAiReply: true,
            content,
            nowUtc
        );

        message.RaiseDomainEvent(new AiReplyFailedDomainEvent(noteId, triggeringChatMessageId));

        return message;
    }

    public static ChatMessage Rehydrate(
        Guid id,
        Guid noteId,
        Guid? authorAppUserId,
        bool isAiReply,
        string content,
        DateTime createdAt
    )
    {
        return new(id, noteId, authorAppUserId, isAiReply, content, createdAt);
    }

    [GeneratedRegex(@"(?<!\S)@AI(?!\S)")]
    private static partial Regex AiMentionRegex();
}
