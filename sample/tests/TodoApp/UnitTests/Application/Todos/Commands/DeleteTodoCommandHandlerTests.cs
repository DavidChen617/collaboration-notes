using Davish.Result;
using NSubstitute;
using Todo.Application.Absctractions;
using Todo.Application.Todos.Commands.Delete;
using Todo.Domain.Todos;
using TodoAggregate = Todo.Domain.Todos.Todo;

namespace UnitTests.Application.Todos.Commands;

public class DeleteTodoCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IUserContext _userContext;
    private readonly ITodoRepository _todoRepository;
    private readonly DeleteTodoCommandHandler _handler;

    public DeleteTodoCommandHandlerTests()
    {
        _userContext = Substitute.For<IUserContext>();
        _userContext.UserId.Returns(UserId);

        _todoRepository = Substitute.For<ITodoRepository>();

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);

        _handler = new DeleteTodoCommandHandler(_userContext, _todoRepository, timeProvider);
    }

    [Fact]
    public async Task GivenTheTodoDoesNotExist_WhenHandling_ThenReturnsNotFound()
    {
        _todoRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TodoAggregate?)null);

        var result = await _handler.HandleAsync(new DeleteTodoCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task GivenTheTodoBelongsToAnotherUser_WhenHandling_ThenReturnsBadRequest()
    {
        var todo = TodoAggregate.Create(Guid.NewGuid(), "Title", "Description");
        _todoRepository.GetByIdAsync(todo.Id, Arg.Any<CancellationToken>()).Returns(todo);

        var result = await _handler.HandleAsync(new DeleteTodoCommand(todo.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BadRequest, result.Error.Type);
    }

    [Fact]
    public async Task GivenTheTodoIsAlreadyCompleted_WhenHandling_ThenPropagatesTheDomainError()
    {
        var todo = TodoAggregate.Rehydrate(Guid.NewGuid(), UserId, "Title", "Description", DateTime.UtcNow, null);
        _todoRepository.GetByIdAsync(todo.Id, Arg.Any<CancellationToken>()).Returns(todo);

        var result = await _handler.HandleAsync(new DeleteTodoCommand(todo.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Todo.Delete", result.Error.Code);
        await _todoRepository.DidNotReceive().DeleteAsync(Arg.Any<TodoAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenTheTodoIsOwnedAndNotFinalized_WhenHandling_ThenDeletesTheTodo()
    {
        var todo = TodoAggregate.Create(UserId, "Title", "Description");
        _todoRepository.GetByIdAsync(todo.Id, Arg.Any<CancellationToken>()).Returns(todo);
        _todoRepository
            .DeleteAsync(Arg.Any<TodoAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _handler.HandleAsync(new DeleteTodoCommand(todo.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(todo.DeletedOnUtc);
        await _todoRepository.Received(1).DeleteAsync(todo, Arg.Any<CancellationToken>());
    }
}

