namespace CoNotes.Application.Abstractions;

public interface IAiReplyRequestQueue
{
    ValueTask QueueAsync(Guid noteId, Guid triggeringChatMessageId, CancellationToken ct);
}
