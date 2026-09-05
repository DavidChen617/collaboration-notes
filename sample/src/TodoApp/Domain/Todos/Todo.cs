using Todo.Domain.Todos.Events;

namespace Todo.Domain.Todos;

public sealed class Todo : AggregateRoot
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public DateTime? CompletedOnUtc { get; private set; }
    public DateTime? DeletedOnUtc { get; private set; }

    private Todo(
        Guid id,
        Guid userId,
        string title,
        string description,
        DateTime? completedOnUtc,
        DateTime? deletedOnUtc
    )
    {
        Id = id;
        Title = title;
        Description = description;
        CompletedOnUtc = completedOnUtc;
        DeletedOnUtc = deletedOnUtc;
        UserId = userId;
    }

    public static Todo Create(Guid userId, string title, string description)
    {
        var todo = new Todo(Guid.CreateVersion7(), userId, title, description, null, null);

        todo.RaiseDomainEvent(new TodoCreatedDomainEvent(todo.Id));

        return todo;
    }

    public static Todo Rehydrate(
        Guid id,
        Guid userId,
        string title,
        string description,
        DateTime? completedOnUtc,
        DateTime? deletedOnUtc
    )
    {
        return new(id, userId, title, description, completedOnUtc, deletedOnUtc);
    }

    public Result Complete(DateTime completedOnUtc)
    {
        if (CompletedOnUtc is not null)
            return new Error("Todo.Complete", "Todo 已經完成!");

        if (DeletedOnUtc is not null)
            return new Error("Todo.Complete", "Todo 已經被刪除!");

        CompletedOnUtc = completedOnUtc;

        RaiseDomainEvent(new TodoCompletedDomainEvent(Id));

        return Result.Success();
    }

    public Result Delete(DateTime deleteOnUtc)
    {
        if (CompletedOnUtc is not null)
            return new Error("Todo.Delete", "Todo 已經完成!");

        if (DeletedOnUtc is not null)
            return new Error("Todo.Delete", "Todo 已經被刪除!");

        DeletedOnUtc = deleteOnUtc;

        RaiseDomainEvent(new TodoDeletedDomainEvent(Id));

        return Result.Success();
    }
}

