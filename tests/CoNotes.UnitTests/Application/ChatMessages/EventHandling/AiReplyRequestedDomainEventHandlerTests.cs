using CoNotes.Application.Abstractions;
using CoNotes.Application.ChatMessages.EventHandling;
using CoNotes.Domain.ChatMessages.Events;
using NSubstitute;

namespace UnitTests.Application.ChatMessages.EventHandling;

public class AiReplyRequestedDomainEventHandlerTests
{
    [Fact]
    public async Task GivenAiReplyRequested_WhenHandled_ThenBackgroundRequestIsQueued()
    {
        var noteId = Guid.NewGuid();
        var triggeringChatMessageId = Guid.NewGuid();
        var queue = Substitute.For<IAiReplyRequestQueue>();
        var handler = new AiReplyRequestedDomainEventHandler(queue);

        await handler.HandleAsync(
            new AiReplyRequestedDomainEvent(noteId, triggeringChatMessageId),
            CancellationToken.None
        );

        await queue.Received(1).QueueAsync(noteId, triggeringChatMessageId, Arg.Any<CancellationToken>());
    }
}
