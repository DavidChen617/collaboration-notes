using Davish.Result;
using TodoAggregate = Todo.Domain.Todos.Todo;

namespace UnitTests.Domain;

public class TodoTests
{
    [Fact]
    public void GivenValidInput_WhenCreatingATodo_ThenSetsTheInitialState()
    {
        var userId = Guid.NewGuid();

        var todo = TodoAggregate.Create(userId, "Buy milk", "2%, not whole");

        Assert.Equal(userId, todo.UserId);
        Assert.Equal("Buy milk", todo.Title);
        Assert.Equal("2%, not whole", todo.Description);
        Assert.Null(todo.CompletedOnUtc);
        Assert.Null(todo.DeletedOnUtc);
    }

    [Fact]
    public void GivenATodoThatIsNotCompletedOrDeleted_WhenCompleting_ThenSucceeds()
    {
        var todo = TodoAggregate.Create(Guid.NewGuid(), "Title", "Description");
        var completedOnUtc = DateTime.UtcNow;

        var result = todo.Complete(completedOnUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(completedOnUtc, todo.CompletedOnUtc);
    }

    [Fact]
    public void GivenATodoThatIsAlreadyCompleted_WhenCompleting_ThenFails()
    {
        var todo = TodoAggregate.Rehydrate(
            Guid.NewGuid(), Guid.NewGuid(), "Title", "Description", DateTime.UtcNow, null);

        var result = todo.Complete(DateTime.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal("Todo.Complete", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void GivenATodoThatIsAlreadyDeleted_WhenCompleting_ThenFails()
    {
        var todo = TodoAggregate.Rehydrate(
            Guid.NewGuid(), Guid.NewGuid(), "Title", "Description", null, DateTime.UtcNow);

        var result = todo.Complete(DateTime.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal("Todo.Complete", result.Error.Code);
    }

    [Fact]
    public void GivenATodoThatIsNotCompletedOrDeleted_WhenDeleting_ThenSucceeds()
    {
        var todo = TodoAggregate.Create(Guid.NewGuid(), "Title", "Description");
        var deletedOnUtc = DateTime.UtcNow;

        var result = todo.Delete(deletedOnUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(deletedOnUtc, todo.DeletedOnUtc);
    }

    [Fact]
    public void GivenATodoThatIsAlreadyCompleted_WhenDeleting_ThenFails()
    {
        var todo = TodoAggregate.Rehydrate(
            Guid.NewGuid(), Guid.NewGuid(), "Title", "Description", DateTime.UtcNow, null);

        var result = todo.Delete(DateTime.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal("Todo.Delete", result.Error.Code);
    }

    [Fact]
    public void GivenATodoThatIsAlreadyDeleted_WhenDeleting_ThenFails()
    {
        var todo = TodoAggregate.Rehydrate(
            Guid.NewGuid(), Guid.NewGuid(), "Title", "Description", null, DateTime.UtcNow);

        var result = todo.Delete(DateTime.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal("Todo.Delete", result.Error.Code);
    }
}

