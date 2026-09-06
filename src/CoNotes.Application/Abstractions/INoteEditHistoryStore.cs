namespace CoNotes.Application.Abstractions;

/// <summary>
/// 儲存筆記即時協作編輯的 Yjs 更新紀錄與快照。這裡完全不理解 Yjs binary 的內容——
/// 只負責依序存取/回放, 對 Yjs 內容的合併/重建邏輯留給前端用 Yjs 自己的 API 做
/// (design.md decision 1)。快照的產生同樣是前端主動上傳目前合併好的完整狀態
/// (<see cref="SaveSnapshotAsync"/>), 不是後端自己合併。
/// </summary>
public interface INoteEditHistoryStore
{
    /// <summary>
    /// 附加一筆 Yjs update。回傳目前累積(尚未被快照涵蓋)的更新筆數是否已超過壓縮門檻,
    /// 供呼叫端決定要不要請前端上傳一份新快照。
    /// </summary>
    Task<bool> AppendUpdateAsync(Guid noteId, byte[] updatePayload, CancellationToken ct);

    /// <summary>
    /// 記錄一份由前端算好的完整合併快照, 並清除已被這份快照涵蓋的舊更新紀錄。
    /// </summary>
    Task SaveSnapshotAsync(Guid noteId, byte[] snapshotPayload, CancellationToken ct);

    /// <summary>
    /// 回放出 <paramref name="atUtc"/> 這個時間點當下的筆記狀態:最近一個(在該時間點之前建立的)快照,
    /// 加上快照之後、且發生在該時間點之前的所有更新紀錄, 依序排列。base 快照不存在時回傳 null
    /// (代表要從空白文件開始疊加)。
    /// </summary>
    Task<NoteEditHistory> GetHistoryUpToAsync(Guid noteId, DateTime atUtc, CancellationToken ct);
}

public sealed record NoteEditHistory(byte[]? BaseSnapshotPayload, IReadOnlyList<byte[]> SubsequentUpdatePayloads);
