using CoNotes.Domain.ChatMessages;

namespace CoNotes.Infrastructure.ChatMessages;

internal sealed class ChatMessageRepository(AppDbContext appDbContext) : IChatMessageRepository
{
    public async Task<Result> AddAsync(ChatMessage chatMessage, CancellationToken ct)
    {
        var param = new
        {
            chatMessage.Id,
            chatMessage.NoteId,
            chatMessage.AuthorAppUserId,
            chatMessage.IsAiReply,
            chatMessage.Content,
            chatMessage.CreatedAt,
        };
        var command = new CommandDefinition(
            $"""
            insert into chat_messages (
                id,
                note_id,
                author_app_user_id,
                is_ai_reply,
                content,
                created_at
            )
            values (
                @{nameof(param.Id)},
                @{nameof(param.NoteId)},
                @{nameof(param.AuthorAppUserId)},
                @{nameof(param.IsAiReply)},
                @{nameof(param.Content)},
                @{nameof(param.CreatedAt)}
            );
            """,
            param,
            cancellationToken: ct,
            transaction: appDbContext.Transaction
        );
        var connection = await appDbContext.GetDbConnectionAsync(ct);

        await connection.ExecuteAsync(command);
        appDbContext.TrackAggregateRoot(chatMessage);

        return Result.Success();
    }

    public async Task<IReadOnlyList<ChatMessage>> GetRecentByNoteIdAsync(
        Guid noteId,
        int limit,
        CancellationToken ct
    )
    {
        var param = new { NoteId = noteId, Limit = limit };
        var command = new CommandDefinition(
            $"""
            select
                id as {nameof(ChatMessageRow.Id)},
                note_id as {nameof(ChatMessageRow.NoteId)},
                author_app_user_id as {nameof(ChatMessageRow.AuthorAppUserId)},
                is_ai_reply as {nameof(ChatMessageRow.IsAiReply)},
                content as {nameof(ChatMessageRow.Content)},
                created_at as {nameof(ChatMessageRow.CreatedAt)}
            from chat_messages
            where note_id = @{nameof(param.NoteId)}
            order by created_at desc, id desc
            limit @{nameof(param.Limit)};
            """,
            param,
            cancellationToken: ct,
            transaction: appDbContext.Transaction
        );
        var connection = await appDbContext.GetDbConnectionAsync(ct);
        var rows = await connection.QueryAsync<ChatMessageRow>(command);

        return rows
            .Reverse()
            .Select(row => ChatMessage.Rehydrate(
                row.Id,
                row.NoteId,
                row.AuthorAppUserId,
                row.IsAiReply,
                row.Content,
                row.CreatedAt
            ))
            .ToArray();
    }

    private sealed record ChatMessageRow(
        Guid Id,
        Guid NoteId,
        Guid? AuthorAppUserId,
        bool IsAiReply,
        string Content,
        DateTime CreatedAt
    );
}
