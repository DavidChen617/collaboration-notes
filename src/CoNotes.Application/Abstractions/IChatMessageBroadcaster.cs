using CoNotes.Domain.ChatMessages;

namespace CoNotes.Application.Abstractions;

public interface IChatMessageBroadcaster
{
    Task BroadcastAsync(ChatMessage chatMessage, CancellationToken ct);
}
