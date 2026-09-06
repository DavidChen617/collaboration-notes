using CoNotes.Domain.Notes;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace CoNotes.Infrastructure.Notes;

internal sealed class NoteRepository(AppDbContext appDbContext) : INoteRepository
{
    public async Task<NoteAggregate?> GetByIdAsync(Guid noteId, CancellationToken ct)
    {
        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var noteCmd = new CommandDefinition(
            $"""
            select
                id as {nameof(NoteRow.Id)},
                owner_app_user_id as {nameof(NoteRow.OwnerAppUserId)},
                title as {nameof(NoteRow.Title)},
                content as {nameof(NoteRow.Content)},
                created_at as {nameof(NoteRow.CreatedOnUtc)},
                updated_at as {nameof(NoteRow.UpdatedOnUtc)}
            from notes
            where id = @NoteId;
            """,
            new { NoteId = noteId },
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var row = await connection.QuerySingleOrDefaultAsync<NoteRow>(noteCmd);

        if (row is null)
            return null;

        var linkedNoteIds = await GetLinkedNoteIdsAsync(noteId, ct);

        return NoteAggregate.Rehydrate(row.Id, row.OwnerAppUserId, row.Title, row.Content, row.CreatedOnUtc, row.UpdatedOnUtc, linkedNoteIds);
    }

    public async Task<Result> AddAsync(NoteAggregate note, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"""
            insert into notes (id, owner_app_user_id, title, content, created_at, updated_at)
            values (@{nameof(note.Id)}, @{nameof(note.OwnerAppUserId)}, @{nameof(note.Title)},
                    @{nameof(note.Content)}, @{nameof(note.CreatedOnUtc)}, @{nameof(note.UpdatedOnUtc)});
            """,
            note,
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        await connection.ExecuteAsync(cmd);

        await ReplaceLinksAsync(note, ct);

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
                updated_at = @{nameof(note.UpdatedOnUtc)}
            where id = @{nameof(note.Id)};
            """,
            note,
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        await connection.ExecuteAsync(cmd);

        await ReplaceLinksAsync(note, ct);

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

        var cmd = new CommandDefinition(
            """
            select id
            from notes
            where owner_app_user_id = @OwnerAppUserId
              and id = any(@CandidateNoteIds);
            """,
            new { OwnerAppUserId = ownerAppUserId, CandidateNoteIds = candidateNoteIds.ToArray() },
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var ownedNoteIds = await connection.QueryAsync<Guid>(cmd);

        return ownedNoteIds.ToHashSet();
    }

    private async Task<IReadOnlyCollection<Guid>> GetLinkedNoteIdsAsync(Guid noteId, CancellationToken ct)
    {
        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var cmd = new CommandDefinition(
            "select target_note_id from note_links where source_note_id = @NoteId;",
            new { NoteId = noteId },
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var targetNoteIds = await connection.QueryAsync<Guid>(cmd);

        return [.. targetNoteIds];
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
        DateTime UpdatedOnUtc
    );
}
