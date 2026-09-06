using CoNotes.Domain.ChatMessages;

namespace CoNotes.Application.ChatMessages.Commands.GenerateAiReply;

internal sealed class GenerateAiReplyCommandHandler(
    INoteRepository noteRepository,
    IChatMessageRepository chatMessageRepository,
    IEnumerable<IAiChatProvider> providers,
    IChatMessageBroadcaster chatMessageBroadcaster,
    TimeProvider timeProvider
) : ICommandHandler<GenerateAiReplyCommand, Result<GenerateAiReplyDto>>
{
    internal const int MaxNoteContentLength = 12_000;
    internal const int RecentMessageLimit = 20;
    internal const string FailureMessage = "AI 目前無法回應，請稍後再試。";

    public async Task<Result<GenerateAiReplyDto>> HandleAsync(
        GenerateAiReplyCommand command,
        CancellationToken cancellationToken
    )
    {
        var note = await noteRepository.GetByIdAsync(command.NoteId, cancellationToken);

        if (note is null)
            return new Error("ChatMessage.GenerateAiReply", "找不到筆記!", ErrorType.NotFound);

        var recentMessages = await chatMessageRepository.GetRecentByNoteIdAsync(
            command.NoteId,
            RecentMessageLimit,
            cancellationToken
        );
        var context = new AiChatContext(
            note.Content[..Math.Min(note.Content.Length, MaxNoteContentLength)],
            recentMessages
                .Select(message => new AiChatContextMessage(
                    message.AuthorAppUserId,
                    message.IsAiReply,
                    message.Content
                ))
                .ToArray()
        );

        foreach (var provider in providers)
        {
            var reply = await provider.TryGetReplyAsync(context, cancellationToken);
            if (reply is null || string.IsNullOrWhiteSpace(reply.Content))
                continue;

            var aiReply = ChatMessage.CreateAiReply(
                command.NoteId,
                reply.Content,
                provider.Name,
                timeProvider.GetUtcNow().UtcDateTime
            );
            var addResult = await chatMessageRepository.AddAsync(aiReply, cancellationToken);
            if (!addResult.IsSuccess)
                return addResult.Error;

            await chatMessageBroadcaster.BroadcastAsync(aiReply, cancellationToken);

            return new GenerateAiReplyDto(
                aiReply.Id,
                aiReply.NoteId,
                aiReply.Content,
                provider.Name
            );
        }

        var failureReply = ChatMessage.CreateAiFailure(
            command.NoteId,
            command.TriggeringChatMessageId,
            FailureMessage,
            timeProvider.GetUtcNow().UtcDateTime
        );
        var failureAddResult = await chatMessageRepository.AddAsync(failureReply, cancellationToken);
        if (!failureAddResult.IsSuccess)
            return failureAddResult.Error;

        await chatMessageBroadcaster.BroadcastAsync(failureReply, cancellationToken);

        return new GenerateAiReplyDto(
            failureReply.Id,
            failureReply.NoteId,
            failureReply.Content,
            ProviderUsed: null
        );
    }
}
