namespace CoNotes.Application.Notes.Queries.GetGraph;

internal sealed class GetNoteGraphQueryHandler(
    IUserContext userContext,
    IDbConnectionFactory dbConnectionFactory
) : IQueryHandler<GetNoteGraphQuery, Result<NoteGraphDto>>
{
    public async Task<Result<NoteGraphDto>> HandleAsync(GetNoteGraphQuery query, CancellationToken cancellationToken)
    {
        var ownerAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);
        var param = new { OwnerAppUserId = ownerAppUserId };

        var sql = $"""
            select
                id as NoteId,
                title as Title
            from notes
            where owner_app_user_id = @{nameof(param.OwnerAppUserId)};

            select
                source_note_id as SourceNoteId,
                target_note_id as TargetNoteId
            from note_links
            where source_note_id in (select id from notes where owner_app_user_id = @{nameof(param.OwnerAppUserId)});
            """;

        using var connection = await dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var command = new CommandDefinition(sql, param, cancellationToken: cancellationToken);

        using var gridReader = await connection.QueryMultipleAsync(command);

        var nodes = await gridReader.ReadAsync<NoteGraphNode>();
        var edges = await gridReader.ReadAsync<NoteGraphEdge>();

        return new NoteGraphDto([.. nodes], [.. edges]);
    }
}
