namespace CoNotes.Application.ChatMessages.Commands.GenerateAiReply;

public sealed record GenerateAiReplyCommand(
    Guid NoteId,
    Guid TriggeringChatMessageId
) : ICommand<Result<GenerateAiReplyDto>>;

public sealed record GenerateAiReplyDto(
    Guid ChatMessageId,
    Guid NoteId,
    string Content,
    string? ProviderUsed
);
