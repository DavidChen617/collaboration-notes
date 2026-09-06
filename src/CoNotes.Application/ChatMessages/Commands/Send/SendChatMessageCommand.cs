namespace CoNotes.Application.ChatMessages.Commands.Send;

public sealed record SendChatMessageCommand(
    Guid NoteId,
    string Content,
    Guid? AuthorAppUserId = null
) : ICommand<Result<SendChatMessageDto>>;

public sealed record SendChatMessageDto(
    Guid ChatMessageId,
    Guid NoteId,
    Guid? AuthorAppUserId,
    bool IsAiReply,
    string Content,
    DateTime CreatedAt
);
