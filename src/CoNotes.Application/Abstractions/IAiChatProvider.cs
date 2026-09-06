namespace CoNotes.Application.Abstractions;

public interface IAiChatProvider
{
    string Name { get; }
    Task<AiChatReply?> TryGetReplyAsync(AiChatContext context, CancellationToken ct);
}

public sealed record AiChatContext(
    string NoteContent,
    IReadOnlyList<AiChatContextMessage> RecentMessages
);

public sealed record AiChatContextMessage(
    Guid? AuthorAppUserId,
    bool IsAiReply,
    string Content
);

public sealed record AiChatReply(string Content);
