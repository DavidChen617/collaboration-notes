using CoNotes.Application.Abstractions;
using CoNotes.Domain.ChatMessages;
using Microsoft.AspNetCore.SignalR;

namespace CoNotes.Api.Hubs;

internal sealed class ChatMessageBroadcaster(
    IHubContext<ChatHub> hubContext
) : IChatMessageBroadcaster
{
    public Task BroadcastAsync(ChatMessage chatMessage, CancellationToken ct)
    {
        return hubContext.Clients
            .Group(ChatHub.GroupName(chatMessage.NoteId))
            .SendAsync(
                "ReceiveMessage",
                new
                {
                    ChatMessageId = chatMessage.Id,
                    chatMessage.NoteId,
                    chatMessage.AuthorAppUserId,
                    chatMessage.IsAiReply,
                    chatMessage.Content,
                    chatMessage.CreatedAt,
                },
                ct
            );
    }
}
