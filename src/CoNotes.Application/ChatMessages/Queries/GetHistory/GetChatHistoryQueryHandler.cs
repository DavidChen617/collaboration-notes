namespace CoNotes.Application.ChatMessages.Queries.GetHistory;

internal sealed class GetChatHistoryQueryHandler(
    IUserContext userContext,
    IDbConnectionFactory dbConnectionFactory
) : IQueryHandler<GetChatHistoryQuery, Result<GetChatHistoryDto>>
{
    private const int HistoryLimit = 50;

    public async Task<Result<GetChatHistoryDto>> HandleAsync(
        GetChatHistoryQuery query,
        CancellationToken cancellationToken
    )
    {
        var requestingAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);
        var accessParam = new { query.NoteId, RequestingAppUserId = requestingAppUserId };
        var accessSql = $"""
            select exists (
                select 1
                from notes
                where id = @{nameof(accessParam.NoteId)}
                  and (
                      owner_app_user_id = @{nameof(accessParam.RequestingAppUserId)}
                      or exists (
                          select 1
                          from note_collaborators
                          where note_id = notes.id
                            and app_user_id = @{nameof(accessParam.RequestingAppUserId)}
                      )
                  )
            );
            """;

        using var connection = await dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var canAccess = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                accessSql,
                accessParam,
                cancellationToken: cancellationToken
            )
        );

        if (!canAccess)
            return new Error("ChatMessage.GetHistory", "使用者沒有權限存取這篇筆記的聊天室!", ErrorType.BadRequest);

        var historyParam = new { query.NoteId, HistoryLimit };
        var historySql = $"""
            select *
            from (
                select
                    id as {nameof(ChatMessageItem.ChatMessageId)},
                    author_app_user_id as {nameof(ChatMessageItem.AuthorAppUserId)},
                    is_ai_reply as {nameof(ChatMessageItem.IsAiReply)},
                    content as {nameof(ChatMessageItem.Content)},
                    created_at as {nameof(ChatMessageItem.CreatedAt)}
                from chat_messages
                where note_id = @{nameof(historyParam.NoteId)}
                order by created_at desc, id desc
                limit @{nameof(historyParam.HistoryLimit)}
            ) recent_messages
            order by {nameof(ChatMessageItem.CreatedAt)} asc, {nameof(ChatMessageItem.ChatMessageId)} asc;
            """;
        var messages = await connection.QueryAsync<ChatMessageItem>(
            new CommandDefinition(
                historySql,
                historyParam,
                cancellationToken: cancellationToken
            )
        );

        return new GetChatHistoryDto([.. messages]);
    }
}
