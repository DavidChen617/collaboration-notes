namespace CoNotes.Infrastructure.Notes;

internal sealed class NoteEditHistoryStore(IDbConnectionFactory dbConnectionFactory, TimeProvider timeProvider) : INoteEditHistoryStore
{
    private const int CompactionThreshold = 200;

    public async Task<bool> AppendUpdateAsync(Guid noteId, byte[] updatePayload, CancellationToken ct)
    {
        using var connection = await dbConnectionFactory.CreateConnectionAsync(ct);

        var param = new { NoteId = noteId, UpdatePayload = updatePayload, CreatedAt = timeProvider.GetUtcNow().UtcDateTime };

        // 下一個序號要同時考慮 note_updates 跟 note_snapshots 兩張表的最大值 - 光看
        // note_updates 不夠, 因為壓縮後這張表可能被清空, 單看它會讓序號從頭算起、
        // 跟 note_snapshots 記錄的 cutoff 序號撞號。
        var insertCmd = new CommandDefinition(
            $"""
            insert into note_updates (note_id, sequence_number, update_payload, created_at)
            values (
                @{nameof(param.NoteId)},
                greatest(
                    coalesce((select max(sequence_number) from note_updates where note_id = @{nameof(param.NoteId)}), 0),
                    coalesce((select max(sequence_number) from note_snapshots where note_id = @{nameof(param.NoteId)}), 0)
                ) + 1,
                @{nameof(param.UpdatePayload)},
                @{nameof(param.CreatedAt)}
            );
            """,
            param,
            cancellationToken: ct);

        await connection.ExecuteAsync(insertCmd);

        var countParam = new { NoteId = noteId };

        var countCmd = new CommandDefinition(
            $"select count(*) from note_updates where note_id = @{nameof(countParam.NoteId)};",
            countParam,
            cancellationToken: ct);

        var pendingUpdateCount = await connection.ExecuteScalarAsync<long>(countCmd);

        return pendingUpdateCount > CompactionThreshold;
    }

    public async Task SaveSnapshotAsync(Guid noteId, byte[] snapshotPayload, CancellationToken ct)
    {
        using var connection = await dbConnectionFactory.CreateConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var cutoffParam = new { NoteId = noteId };

        var cutoffCmd = new CommandDefinition(
            $"select coalesce(max(sequence_number), 0) from note_updates where note_id = @{nameof(cutoffParam.NoteId)};",
            cutoffParam,
            transaction: transaction,
            cancellationToken: ct);

        var cutoffSequenceNumber = await connection.ExecuteScalarAsync<long>(cutoffCmd);

        var snapshotParam = new
        {
            NoteId = noteId,
            SequenceNumber = cutoffSequenceNumber,
            SnapshotPayload = snapshotPayload,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        var insertCmd = new CommandDefinition(
            $"""
            insert into note_snapshots (note_id, sequence_number, snapshot_payload, created_at)
            values (@{nameof(snapshotParam.NoteId)}, @{nameof(snapshotParam.SequenceNumber)},
                    @{nameof(snapshotParam.SnapshotPayload)}, @{nameof(snapshotParam.CreatedAt)});
            """,
            snapshotParam,
            transaction: transaction,
            cancellationToken: ct);

        await connection.ExecuteAsync(insertCmd);

        var deleteParam = new { NoteId = noteId, CutoffSequenceNumber = cutoffSequenceNumber };

        var deleteCmd = new CommandDefinition(
            $"""
            delete from note_updates
            where note_id = @{nameof(deleteParam.NoteId)} and sequence_number <= @{nameof(deleteParam.CutoffSequenceNumber)};
            """,
            deleteParam,
            transaction: transaction,
            cancellationToken: ct);

        await connection.ExecuteAsync(deleteCmd);

        transaction.Commit();
    }

    public async Task<NoteEditHistory> GetHistoryUpToAsync(Guid noteId, DateTime atUtc, CancellationToken ct)
    {
        using var connection = await dbConnectionFactory.CreateConnectionAsync(ct);

        var param = new { NoteId = noteId, AtUtc = atUtc };

        var snapshotCmd = new CommandDefinition(
            $"""
            select
                snapshot_payload as {nameof(SnapshotRow.SnapshotPayload)},
                sequence_number as {nameof(SnapshotRow.SequenceNumber)}
            from note_snapshots
            where note_id = @{nameof(param.NoteId)} and created_at <= @{nameof(param.AtUtc)}
            order by sequence_number desc
            limit 1;
            """,
            param,
            cancellationToken: ct);

        var snapshotRow = await connection.QuerySingleOrDefaultAsync<SnapshotRow>(snapshotCmd);
        var cutoffSequenceNumber = snapshotRow?.SequenceNumber ?? 0;

        var updatesParam = new { NoteId = noteId, AtUtc = atUtc, CutoffSequenceNumber = cutoffSequenceNumber };

        var updatesCmd = new CommandDefinition(
            $"""
            select update_payload
            from note_updates
            where note_id = @{nameof(updatesParam.NoteId)}
              and sequence_number > @{nameof(updatesParam.CutoffSequenceNumber)}
              and created_at <= @{nameof(updatesParam.AtUtc)}
            order by sequence_number;
            """,
            updatesParam,
            cancellationToken: ct);

        var updatePayloads = await connection.QueryAsync<byte[]>(updatesCmd);

        return new NoteEditHistory(snapshotRow?.SnapshotPayload, [.. updatePayloads]);
    }

    private sealed record SnapshotRow(byte[] SnapshotPayload, long SequenceNumber);
}
