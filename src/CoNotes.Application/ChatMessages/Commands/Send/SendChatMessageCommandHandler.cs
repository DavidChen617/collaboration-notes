using CoNotes.Domain.ChatMessages;

namespace CoNotes.Application.ChatMessages.Commands.Send;

internal sealed class SendChatMessageCommandHandler(
    IUserContext userContext,
    INoteRepository noteRepository,
    IChatMessageRepository chatMessageRepository,
    IChatMessageBroadcaster chatMessageBroadcaster,
    TimeProvider timeProvider
) : ICommandHandler<SendChatMessageCommand, Result<SendChatMessageDto>>
{
    public async Task<Result<SendChatMessageDto>> HandleAsync(
        SendChatMessageCommand command,
        CancellationToken cancellationToken
    )
    {
        var note = await noteRepository.GetByIdAsync(command.NoteId, cancellationToken);

        if (note is null)
            return new Error("ChatMessage.Send", "找不到筆記!", ErrorType.NotFound);

        var authorAppUserId = command.AuthorAppUserId
            ?? await userContext.GetAppUserIdAsync(cancellationToken);

        if (!note.IsAccessibleBy(authorAppUserId))
            return new Error("ChatMessage.Send", "使用者沒有權限存取這篇筆記的聊天室!", ErrorType.BadRequest);

        var chatMessage = ChatMessage.Create(
            command.NoteId,
            authorAppUserId,
            command.Content,
            timeProvider.GetUtcNow().UtcDateTime
        );

        var addResult = await chatMessageRepository.AddAsync(chatMessage, cancellationToken);
        if (!addResult.IsSuccess)
            return addResult.Error;

        await chatMessageBroadcaster.BroadcastAsync(chatMessage, cancellationToken);

        return new SendChatMessageDto(
            chatMessage.Id,
            chatMessage.NoteId,
            chatMessage.AuthorAppUserId,
            chatMessage.IsAiReply,
            chatMessage.Content,
            chatMessage.CreatedAt
        );
    }
}
