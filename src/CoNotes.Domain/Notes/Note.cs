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
    /// Replaces this note's outgoing wikilinks with <paramref name="requestedTargetNoteIds"/>, rejecting the
    /// whole call if any requested target isn't in <paramref name="ownedTargetNoteIds"/> (the subset of
    /// requested targets the caller has already confirmed belong to this note's owner). Raises
    /// <see cref="NoteLinkedToDomainEvent"/>/<see cref="NoteLinkRemovedDomainEvent"/> for the diff against the
    /// previously resolved set.
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
