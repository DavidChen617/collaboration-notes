using CoNotes.Domain.Notes;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace CoNotes.Infrastructure.Notes;

internal sealed class NoteRepository(AppDbContext appDbContext) : INoteRepository
{
    public async Task<NoteAggregate?> GetByIdAsync(Guid noteId, CancellationToken ct)
    {
        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var param = new { NoteId = noteId };

        var sql = $"""
            select
                id as {nameof(NoteRow.Id)},
                owner_app_user_id as {nameof(NoteRow.OwnerAppUserId)},
                title as {nameof(NoteRow.Title)},
                content as {nameof(NoteRow.Content)},
                created_at as {nameof(NoteRow.CreatedOnUtc)},
                updated_at as {nameof(NoteRow.UpdatedOnUtc)},
                share_token as {nameof(NoteRow.ShareToken)}
            from notes
            where id = @{nameof(param.NoteId)};

            select target_note_id from note_links where source_note_id = @{nameof(param.NoteId)};

            select app_user_id from note_collaborators where note_id = @{nameof(param.NoteId)};
            """;

        var cmd = new CommandDefinition(sql, param, cancellationToken: ct, transaction: appDbContext.Transaction);

        using var gridReader = await connection.QueryMultipleAsync(cmd);

        return await RehydrateAsync(gridReader);
    }

    public async Task<NoteAggregate?> GetByShareTokenAsync(Guid shareToken, CancellationToken ct)
    {
        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var param = new { ShareToken = shareToken };

        var sql = $"""
            select
                id as {nameof(NoteRow.Id)},
                owner_app_user_id as {nameof(NoteRow.OwnerAppUserId)},
                title as {nameof(NoteRow.Title)},
                content as {nameof(NoteRow.Content)},
                created_at as {nameof(NoteRow.CreatedOnUtc)},
                updated_at as {nameof(NoteRow.UpdatedOnUtc)},
                share_token as {nameof(NoteRow.ShareToken)}
            from notes
            where share_token = @{nameof(param.ShareToken)};

            select target_note_id from note_links
            where source_note_id = (select id from notes where share_token = @{nameof(param.ShareToken)});

            select app_user_id from note_collaborators
            where note_id = (select id from notes where share_token = @{nameof(param.ShareToken)});
            """;

        var cmd = new CommandDefinition(sql, param, cancellationToken: ct, transaction: appDbContext.Transaction);

        using var gridReader = await connection.QueryMultipleAsync(cmd);

        return await RehydrateAsync(gridReader);
    }

    private static async Task<NoteAggregate?> RehydrateAsync(SqlMapper.GridReader gridReader)
    {
        var row = await gridReader.ReadSingleOrDefaultAsync<NoteRow>();
        var linkedNoteIds = await gridReader.ReadAsync<Guid>();
        var collaboratorAppUserIds = await gridReader.ReadAsync<Guid>();

        if (row is null)
            return null;

        return NoteAggregate.Rehydrate(
            row.Id, row.OwnerAppUserId, row.Title, row.Content, row.CreatedOnUtc, row.UpdatedOnUtc,
            [.. linkedNoteIds], row.ShareToken, [.. collaboratorAppUserIds]);
    }

    public async Task<Result> AddAsync(NoteAggregate note, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"""
            insert into notes (id, owner_app_user_id, title, content, created_at, updated_at, share_token)
            values (@{nameof(note.Id)}, @{nameof(note.OwnerAppUserId)}, @{nameof(note.Title)},
                    @{nameof(note.Content)}, @{nameof(note.CreatedOnUtc)}, @{nameof(note.UpdatedOnUtc)},
                    @{nameof(note.ShareToken)});
            """,
            note,
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        await connection.ExecuteAsync(cmd);

        await ReplaceLinksAsync(note, ct);
        await ReplaceCollaboratorsAsync(note, ct);

        appDbContext.TrackAggregateRoot(note);

        return Result.Success();
    }

    public async Task<Result> UpdateAsync(NoteAggregate note, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"""
            update notes
            set title = @{nameof(note.Title)},
                content = @{nameof(note.Content)},
                updated_at = @{nameof(note.UpdatedOnUtc)},
                share_token = @{nameof(note.ShareToken)}
            where id = @{nameof(note.Id)};
            """,
            note,
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        await connection.ExecuteAsync(cmd);

        await ReplaceLinksAsync(note, ct);
        await ReplaceCollaboratorsAsync(note, ct);

        appDbContext.TrackAggregateRoot(note);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(NoteAggregate note, CancellationToken ct)
    {
        // note_links 的 source_note_id 和 target_note_id 都設定了 `on delete cascade`,
        // 所以刪除 note 這一列本身就會一併移除所有以它為 source 或 target 的連結。
        var cmd = new CommandDefinition(
            $"""
            delete from notes
            where id = @{nameof(note.Id)};
            """,
            note,
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        await connection.ExecuteAsync(cmd);

        appDbContext.TrackAggregateRoot(note);

        return Result.Success();
    }

    public async Task<IReadOnlySet<Guid>> FindOwnedNoteIdsAsync(Guid ownerAppUserId, IReadOnlyCollection<Guid> candidateNoteIds, CancellationToken ct)
    {
        if (candidateNoteIds.Count == 0)
            return new HashSet<Guid>();

        var param = new { OwnerAppUserId = ownerAppUserId, CandidateNoteIds = candidateNoteIds.ToArray() };

        var cmd = new CommandDefinition(
            $"""
            select id
            from notes
            where owner_app_user_id = @{nameof(param.OwnerAppUserId)}
              and id = any(@{nameof(param.CandidateNoteIds)});
            """,
            param,
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var ownedNoteIds = await connection.QueryAsync<Guid>(cmd);

        return ownedNoteIds.ToHashSet();
    }

    private async Task ReplaceCollaboratorsAsync(NoteAggregate note, CancellationToken ct)
    {
        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var deleteCmd = new CommandDefinition(
            $"delete from note_collaborators where note_id = @{nameof(note.Id)};",
            note,
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        await connection.ExecuteAsync(deleteCmd);

        if (note.CollaboratorAppUserIds.Count == 0)
            return;

        var insertCmd = new CommandDefinition(
            $"""
            insert into note_collaborators (note_id, app_user_id)
            values (@{nameof(NoteAggregate.Id)}, @AppUserId);
            """,
            note.CollaboratorAppUserIds.Select(appUserId => new { note.Id, AppUserId = appUserId }),
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        await connection.ExecuteAsync(insertCmd);
    }

    private async Task ReplaceLinksAsync(NoteAggregate note, CancellationToken ct)
    {
        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var deleteCmd = new CommandDefinition(
            $"delete from note_links where source_note_id = @{nameof(note.Id)};",
            note,
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        await connection.ExecuteAsync(deleteCmd);

        if (note.LinkedNoteIds.Count == 0)
            return;

        var insertCmd = new CommandDefinition(
            $"""
            insert into note_links (source_note_id, target_note_id)
            values (@{nameof(NoteAggregate.Id)}, @TargetNoteId);
            """,
            note.LinkedNoteIds.Select(targetNoteId => new { note.Id, TargetNoteId = targetNoteId }),
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        await connection.ExecuteAsync(insertCmd);
    }

    private sealed record NoteRow(
        Guid Id,
        Guid OwnerAppUserId,
        string Title,
        string Content,
        DateTime CreatedOnUtc,
        DateTime UpdatedOnUtc,
        Guid? ShareToken
    );
}
