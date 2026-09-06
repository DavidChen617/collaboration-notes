using CoNotes.Domain.Notes.Events;

namespace CoNotes.Domain.Notes;

public sealed class Note : AggregateRoot
{
    private HashSet<Guid> _linkedNoteIds = [];
    private readonly HashSet<Guid> _collaboratorAppUserIds = [];

    public Guid OwnerAppUserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime UpdatedOnUtc { get; private set; }
    public ShareLinkToken? ShareToken { get; private set; }
    public IReadOnlyCollection<Guid> LinkedNoteIds => _linkedNoteIds;
    public IReadOnlyCollection<Guid> CollaboratorAppUserIds => _collaboratorAppUserIds;

    private Note(
        Guid id,
        Guid ownerAppUserId,
        string title,
        string content,
        DateTime createdAt,
        DateTime updatedAt,
        IEnumerable<Guid>? linkedNoteIds = null,
        ShareLinkToken? shareToken = null,
        IEnumerable<Guid>? collaboratorAppUserIds = null
    )
    {
        Id = id;
        OwnerAppUserId = ownerAppUserId;
        Title = title;
        Content = content;
        CreatedOnUtc = createdAt;
        UpdatedOnUtc = updatedAt;
        _linkedNoteIds = linkedNoteIds?.ToHashSet() ?? [];
        ShareToken = shareToken;
        _collaboratorAppUserIds = collaboratorAppUserIds?.ToHashSet() ?? [];
    }

    public static Note Create(Guid ownerAppUserId, string title, string content, DateTime nowUtc)
    {
        var note = new Note(Guid.CreateVersion7(), ownerAppUserId, title, content, nowUtc, nowUtc);

        note.RaiseDomainEvent(new NoteCreatedDomainEvent(note.Id, ownerAppUserId));

        return note;
    }

    public static Note Rehydrate(
        Guid id,
        Guid ownerAppUserId,
        string title,
        string content,
        DateTime createdAt,
        DateTime updatedAt,
        IEnumerable<Guid>? linkedNoteIds = null,
        ShareLinkToken? shareToken = null,
        IEnumerable<Guid>? collaboratorAppUserIds = null
    )
    {
        return new(id, ownerAppUserId, title, content, createdAt, updatedAt, linkedNoteIds, shareToken, collaboratorAppUserIds);
    }

    public bool IsAccessibleBy(Guid appUserId) =>
        appUserId == OwnerAppUserId || _collaboratorAppUserIds.Contains(appUserId);

    public Result Update(Guid requestingAppUserId, string title, string content, DateTime nowUtc)
    {
        if (!IsAccessibleBy(requestingAppUserId))
            return new Error("Note.Update", "使用者沒有權限更新這篇筆記!", ErrorType.BadRequest);

        Title = title;
        Content = content;
        UpdatedOnUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>
    /// 設定這篇筆記的分享連結 token, 取代目前(若有)的 token。token 本身由呼叫者(Application 層)
    /// 產生好再傳進來——「怎麼產生一個不可猜測的值」是技術細節, 不是 Domain 該決定的事,
    /// Domain 只驗證擁有權、記錄狀態、觸發事件。
    /// </summary>
    public Result SetShareLink(Guid requestingAppUserId, ShareLinkToken shareToken)
    {
        if (requestingAppUserId != OwnerAppUserId)
            return new Error("Note.SetShareLink", "使用者沒有權限產生這篇筆記的分享連結!", ErrorType.BadRequest);

        ShareToken = shareToken;

        RaiseDomainEvent(new NoteShareLinkGeneratedDomainEvent(Id, shareToken));

        return Result.Success();
    }

    /// <summary>
    /// 撤銷目前的分享連結(舊 token 立即失效)。只清掉連結本身, 不影響先前已透過連結加入的共編者;
    /// 若要立刻換發新連結, 呼叫端在這之後另外呼叫 <see cref="SetShareLink"/>。
    /// </summary>
    public Result RevokeShareLink(Guid requestingAppUserId)
    {
        if (requestingAppUserId != OwnerAppUserId)
            return new Error("Note.RevokeShareLink", "使用者沒有權限撤銷這篇筆記的分享連結!", ErrorType.BadRequest);

        ShareToken = null;

        RaiseDomainEvent(new NoteShareLinkRevokedDomainEvent(Id));

        return Result.Success();
    }

    /// <summary>
    /// 已登入使用者透過分享連結加入共編者名單。同一條連結被同一使用者重複開啟是 idempotent 的,
    /// 不會重複加入或重複觸發事件;擁有者本人開啟自己的分享連結也是 no-op。
    /// </summary>
    public Result JoinViaShareLink(ShareLinkToken shareToken, Guid joiningAppUserId)
    {
        if (ShareToken is null || ShareToken != shareToken)
            return new Error("Note.JoinViaShareLink", "分享連結無效或已失效!", ErrorType.BadRequest);

        if (joiningAppUserId == OwnerAppUserId || !_collaboratorAppUserIds.Add(joiningAppUserId))
            return Result.Success();

        RaiseDomainEvent(new NoteCollaboratorJoinedDomainEvent(Id, joiningAppUserId));

        return Result.Success();
    }

    public Result RemoveCollaborator(Guid requestingAppUserId, Guid collaboratorAppUserId)
    {
        if (requestingAppUserId != OwnerAppUserId)
            return new Error("Note.RemoveCollaborator", "使用者沒有權限移除共編者!", ErrorType.BadRequest);

        if (!_collaboratorAppUserIds.Remove(collaboratorAppUserId))
            return new Error("Note.RemoveCollaborator", "該使用者不是這篇筆記的共編者!", ErrorType.NotFound);

        RaiseDomainEvent(new NoteCollaboratorRemovedDomainEvent(Id, collaboratorAppUserId));

        return Result.Success();
    }

    /// <summary>
    /// 用 <paramref name="requestedTargetNoteIds"/> 取代這篇筆記目前的所有 outgoing wikilink;
    /// 只要有任何一個目標不在 <paramref name="ownedTargetNoteIds"/>(呼叫者已先確認屬於同一個 owner 的目標子集)中,
    /// 整次呼叫就會被拒絕。會針對與先前已解析集合的差異, 觸發
    /// <see cref="NoteLinkedToDomainEvent"/>/<see cref="NoteLinkRemovedDomainEvent"/>。
    /// </summary>
    public Result ResolveLinks(IReadOnlyCollection<Guid> requestedTargetNoteIds, IReadOnlySet<Guid> ownedTargetNoteIds)
    {
        if (requestedTargetNoteIds.Any(targetNoteId => !ownedTargetNoteIds.Contains(targetNoteId)))
            return new Error("Note.ResolveLinks", "只能連結到自己擁有的筆記!", ErrorType.BadRequest);

        var newLinkedNoteIds = requestedTargetNoteIds.ToHashSet();

        foreach (var addedTargetNoteId in newLinkedNoteIds.Except(_linkedNoteIds))
            RaiseDomainEvent(new NoteLinkedToDomainEvent(Id, addedTargetNoteId));

        foreach (var removedTargetNoteId in _linkedNoteIds.Except(newLinkedNoteIds))
            RaiseDomainEvent(new NoteLinkRemovedDomainEvent(Id, removedTargetNoteId));

        _linkedNoteIds = newLinkedNoteIds;

        return Result.Success();
    }

    public Result Delete(Guid requestingAppUserId)
    {
        if (requestingAppUserId != OwnerAppUserId)
            return new Error("Note.Delete", "使用者沒有權限刪除這篇筆記!", ErrorType.BadRequest);

        RaiseDomainEvent(new NoteDeletedDomainEvent(Id));

        return Result.Success();
    }
}
