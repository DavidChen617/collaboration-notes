using CoNotes.Domain.ChatMessages.Events;
using Davish.SharedKernel;

namespace CoNotes.Application.ChatMessages.EventHandling;

internal sealed class AiReplyRequestedDomainEventHandler(
    IAiReplyRequestQueue queue
) : IDomainEventHandler<AiReplyRequestedDomainEvent>
{
    public async Task HandleAsync(
        AiReplyRequestedDomainEvent notification,
        CancellationToken cancellationToken
    )
    {
        await queue.QueueAsync(
            notification.NoteId,
            notification.TriggeringChatMessageId,
            cancellationToken
        );
    }
}
