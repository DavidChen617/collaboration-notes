using CoNotes.Domain.ChatMessages;
using CoNotes.Domain.ChatMessages.Events;

namespace UnitTests.Domain;

public class ChatMessageTests
{
    [Fact]
    public void GivenMessageWithStandaloneAiMention_WhenChatMessageCreated_ThenRaisesAiReplyRequested()
    {
        var noteId = Guid.NewGuid();
        var authorAppUserId = Guid.NewGuid();

        var message = ChatMessage.Create(noteId, authorAppUserId, "請 @AI 幫我摘要", DateTime.UtcNow);

        var sentEvent = Assert.IsType<ChatMessageSentDomainEvent>(
            Assert.Single(message.DomainEvents, domainEvent => domainEvent is ChatMessageSentDomainEvent)
        );
        Assert.True(sentEvent.ContainsAiMention);

        var requestedEvent = Assert.IsType<AiReplyRequestedDomainEvent>(
            Assert.Single(message.DomainEvents, domainEvent => domainEvent is AiReplyRequestedDomainEvent)
        );
        Assert.Equal(noteId, requestedEvent.NoteId);
        Assert.Equal(message.Id, requestedEvent.TriggeringChatMessageId);
    }

    [Fact]
    public void GivenMessageWithAiSubstringInsideEmail_WhenChatMessageCreated_ThenDoesNotRaiseAiReplyRequested()
    {
        var message = ChatMessage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "請寄到 user@AI.example.com",
            DateTime.UtcNow
        );

        var sentEvent = Assert.IsType<ChatMessageSentDomainEvent>(Assert.Single(message.DomainEvents));
        Assert.False(sentEvent.ContainsAiMention);
        Assert.DoesNotContain(message.DomainEvents, domainEvent => domainEvent is AiReplyRequestedDomainEvent);
    }

    [Fact]
    public void GivenPlainHumanMessage_WhenChatMessageCreated_ThenRaisesChatMessageSentOnly()
    {
        var noteId = Guid.NewGuid();
        var authorAppUserId = Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;

        var message = ChatMessage.Create(noteId, authorAppUserId, "一般聊天訊息", nowUtc);

        Assert.Equal(noteId, message.NoteId);
        Assert.Equal(authorAppUserId, message.AuthorAppUserId);
        Assert.False(message.IsAiReply);
        Assert.Equal("一般聊天訊息", message.Content);
        Assert.Equal(nowUtc, message.CreatedAt);

        var sentEvent = Assert.IsType<ChatMessageSentDomainEvent>(Assert.Single(message.DomainEvents));
        Assert.Equal(message.Id, sentEvent.ChatMessageId);
        Assert.Equal(noteId, sentEvent.NoteId);
        Assert.Equal(authorAppUserId, sentEvent.AuthorAppUserId);
        Assert.False(sentEvent.ContainsAiMention);
    }
}
