using CoNotes.Domain.Notes.Events;

namespace CoNotes.Domain.Notes;

public sealed class Note : AggregateRoot
{
    public Guid OwnerAppUserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Note(
        Guid id,
        Guid ownerAppUserId,
        string title,
        string content,
        DateTime createdAt,
        DateTime updatedAt
    )
    {
        Id = id;
        OwnerAppUserId = ownerAppUserId;
        Title = title;
        Content = content;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Note Create(Guid ownerAppUserId, string title, string content)
    {
        var now = DateTime.UtcNow;
        var note = new Note(Guid.CreateVersion7(), ownerAppUserId, title, content, now, now);

        note.RaiseDomainEvent(new NoteCreatedDomainEvent(note.Id, ownerAppUserId));

        return note;
    }

    public static Note Rehydrate(
        Guid id,
        Guid ownerAppUserId,
        string title,
        string content,
        DateTime createdAt,
        DateTime updatedAt
    )
    {
        return new(id, ownerAppUserId, title, content, createdAt, updatedAt);
    }

    public Result Update(Guid requestingAppUserId, string title, string content)
    {
        if (requestingAppUserId != OwnerAppUserId)
            return new Error("Note.Update", "使用者沒有權限更新這篇筆記!", ErrorType.BadRequest);

        Title = title;
        Content = content;
        UpdatedAt = DateTime.UtcNow;

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
