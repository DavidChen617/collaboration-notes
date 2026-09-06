using System.Threading.Channels;
using CoNotes.Application.Abstractions;
using CoNotes.Application.ChatMessages.Commands.GenerateAiReply;

namespace CoNotes.Api.BackgroundJobs;

internal sealed class AiReplyRequestQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<AiReplyRequestQueue> logger
) : BackgroundService, IAiReplyRequestQueue
{
    private readonly Channel<AiReplyRequest> _channel =
        Channel.CreateUnbounded<AiReplyRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask QueueAsync(
        Guid noteId,
        Guid triggeringChatMessageId,
        CancellationToken ct
    )
    {
        return _channel.Writer.WriteAsync(
            new AiReplyRequest(noteId, triggeringChatMessageId),
            ct
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.SendAsync(
                    new GenerateAiReplyCommand(
                        request.NoteId,
                        request.TriggeringChatMessageId
                    ),
                    stoppingToken
                );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to generate AI reply for chat message {ChatMessageId}",
                    request.TriggeringChatMessageId
                );
            }
        }
    }

    private sealed record AiReplyRequest(Guid NoteId, Guid TriggeringChatMessageId);
}
