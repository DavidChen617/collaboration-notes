using CoNotes.Domain.Notes;
using NoteAggregate = CoNotes.Domain.Notes.Note;

namespace CoNotes.Infrastructure.Notes;

internal sealed class NoteRepository(IDbConnectionFactory dbConnectionFactory) : INoteRepository
{
    public async Task<NoteAggregate?> GetByIdAsync(Guid noteId, CancellationToken ct)
    {
        using var connection = await dbConnectionFactory.CreateConnectionAsync(ct);

        var cmd = new CommandDefinition(
            $"""
            select
                id as {nameof(NoteRow.Id)},
                owner_app_user_id as {nameof(NoteRow.OwnerAppUserId)},
                title as {nameof(NoteRow.Title)},
                content as {nameof(NoteRow.Content)},
                created_at as {nameof(NoteRow.CreatedAt)},
                updated_at as {nameof(NoteRow.UpdatedAt)}
            from notes
            where id = @NoteId;
            """,
            new { NoteId = noteId },
            cancellationToken: ct);

        var row = await connection.QuerySingleOrDefaultAsync<NoteRow>(cmd);

        return row is null
            ? null
            : NoteAggregate.Rehydrate(row.Id, row.OwnerAppUserId, row.Title, row.Content, row.CreatedAt, row.UpdatedAt);
    }

    public async Task<Result> AddAsync(NoteAggregate note, CancellationToken ct)
    {
        using var connection = await dbConnectionFactory.CreateConnectionAsync(ct);

        var cmd = new CommandDefinition(
            $"""
            insert into notes (id, owner_app_user_id, title, content, created_at, updated_at)
            values (@{nameof(note.Id)}, @{nameof(note.OwnerAppUserId)}, @{nameof(note.Title)},
                    @{nameof(note.Content)}, @{nameof(note.CreatedAt)}, @{nameof(note.UpdatedAt)});
            """,
            note,
            cancellationToken: ct);

        await connection.ExecuteAsync(cmd);

        return Result.Success();
    }

    public async Task<Result> UpdateAsync(NoteAggregate note, CancellationToken ct)
    {
        using var connection = await dbConnectionFactory.CreateConnectionAsync(ct);

        var cmd = new CommandDefinition(
            $"""
            update notes
            set title = @{nameof(note.Title)},
                content = @{nameof(note.Content)},
                updated_at = @{nameof(note.UpdatedAt)}
            where id = @{nameof(note.Id)};
            """,
            note,
            cancellationToken: ct);

        await connection.ExecuteAsync(cmd);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(NoteAggregate note, CancellationToken ct)
    {
        using var connection = await dbConnectionFactory.CreateConnectionAsync(ct);

        var cmd = new CommandDefinition(
            $"""
            delete from notes
            where id = @{nameof(note.Id)};
            """,
            note,
            cancellationToken: ct);

        await connection.ExecuteAsync(cmd);

        return Result.Success();
    }

    private sealed record NoteRow(
        Guid Id,
        Guid OwnerAppUserId,
        string Title,
        string Content,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}
