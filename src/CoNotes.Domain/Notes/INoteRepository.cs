namespace CoNotes.Domain.Notes;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(Guid noteId, CancellationToken ct);
    Task<Result> AddAsync(Note note, CancellationToken ct);
    Task<Result> UpdateAsync(Note note, CancellationToken ct);
    Task<Result> DeleteAsync(Note note, CancellationToken ct);

    /// <summary>
    /// 在 <paramref name="candidateNoteIds"/> 之中, 回傳確實存在且屬於
    /// <paramref name="ownerAppUserId"/> 的子集 - 用於呼叫 <see cref="Note.ResolveLinks"/> 前
    /// 驗證 wikilink 的目標。
    /// </summary>
    Task<IReadOnlySet<Guid>> FindOwnedNoteIdsAsync(Guid ownerAppUserId, IReadOnlyCollection<Guid> candidateNoteIds, CancellationToken ct);
}
