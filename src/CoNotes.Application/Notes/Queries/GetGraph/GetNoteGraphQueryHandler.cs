using CoNotes.Application.Abstractions;
using Dapper;

namespace CoNotes.Application.Notes.Queries.GetGraph;

internal sealed class GetNoteGraphQueryHandler(
    IUserContext userContext,
    IDbConnectionFactory dbConnectionFactory
) : IQueryHandler<GetNoteGraphQuery, Result<NoteGraphDto>>
{
    public async Task<Result<NoteGraphDto>> HandleAsync(GetNoteGraphQuery query, CancellationToken cancellationToken)
    {
        const string nodesSql = """
            select
                id as NoteId,
                title as Title
            from notes
            where owner_app_user_id = @OwnerAppUserId
            """;

        const string edgesSql = """
            select
                source_note_id as SourceNoteId,
                target_note_id as TargetNoteId
            from note_links
            where source_note_id in (select id from notes where owner_app_user_id = @OwnerAppUserId)
            """;

        var ownerAppUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        using var connection = await dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var nodesCommand = new CommandDefinition(
            nodesSql, new { OwnerAppUserId = ownerAppUserId }, cancellationToken: cancellationToken);
        var nodes = await connection.QueryAsync<NoteGraphNode>(nodesCommand);

        var edgesCommand = new CommandDefinition(
            edgesSql, new { OwnerAppUserId = ownerAppUserId }, cancellationToken: cancellationToken);
        var edges = await connection.QueryAsync<NoteGraphEdge>(edgesCommand);

        return new NoteGraphDto([.. nodes], [.. edges]);
    }
}
