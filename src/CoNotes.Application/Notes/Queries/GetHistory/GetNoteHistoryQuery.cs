namespace CoNotes.Application.Notes.Queries.GetHistory;

/// <summary>
/// 取得回放某個時間點(<paramref name="AtUtc"/>)筆記內容所需要的資料:一份 base 快照(若有)
/// 加上之後的更新紀錄, 依序疊加即可重建當下內容。也用於前端第一次連上即時協作編輯時,
/// 用 <see cref="DateTime.UtcNow"/> 取得「目前完整狀態」來初始化本地的 Yjs 文件。
/// </summary>
public sealed record GetNoteHistoryQuery(Guid NoteId, DateTime AtUtc) : IQuery<Result<GetNoteHistoryDto>>;

public sealed record GetNoteHistoryDto(byte[]? BaseSnapshot, IReadOnlyList<byte[]> SubsequentUpdates);
