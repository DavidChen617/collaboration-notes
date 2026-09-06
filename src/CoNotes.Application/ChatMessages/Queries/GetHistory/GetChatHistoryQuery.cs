namespace CoNotes.Application.ChatMessages.Queries.GetHistory;

public sealed record GetChatHistoryQuery(Guid NoteId) : IQuery<Result<GetChatHistoryDto>>;

public sealed record GetChatHistoryDto(IReadOnlyList<ChatMessageItem> Messages);

public sealed record ChatMessageItem(
    Guid ChatMessageId,
    Guid? AuthorAppUserId,
    bool IsAiReply,
    string Content,
    DateTime CreatedAt
);
