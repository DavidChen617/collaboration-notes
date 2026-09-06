using CoNotes.Domain.Notes.Events;

namespace CoNotes.Domain.Notes;

public sealed class Note : AggregateRoot
{
    private HashSet<Guid> _linkedNoteIds = [];

    public Guid OwnerAppUserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime UpdatedOnUtc { get; private set; }
    public IReadOnlyCollection<Guid> LinkedNoteIds => _linkedNoteIds;

    private Note(
        Guid id,
        Guid ownerAppUserId,
        string title,
        string content,
        DateTime createdAt,
        DateTime updatedAt,
        IEnumerable<Guid>? linkedNoteIds = null
    )
    {
        Id = id;
        OwnerAppUserId = ownerAppUserId;
        Title = title;
        Content = content;
        CreatedOnUtc = createdAt;
        UpdatedOnUtc = updatedAt;
        _linkedNoteIds = linkedNoteIds?.ToHashSet() ?? [];
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
        IEnumerable<Guid>? linkedNoteIds = null
    )
    {
        return new(id, ownerAppUserId, title, content, createdAt, updatedAt, linkedNoteIds);
    }

    public Result Update(Guid requestingAppUserId, string title, string content, DateTime nowUtc)
    {
        if (requestingAppUserId != OwnerAppUserId)
            return new Error("Note.Update", "使用者沒有權限更新這篇筆記!", ErrorType.BadRequest);

        Title = title;
        Content = content;
        UpdatedOnUtc = nowUtc;

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
