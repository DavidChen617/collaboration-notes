using Davish.Result;
using NSubstitute;
using Todo.Application.Absctractions;
using Todo.Application.Todos.Commands.Complete;
using Todo.Domain.Todos;
using TodoAggregate = Todo.Domain.Todos.Todo;

namespace UnitTests.Application.Todos.Commands;

public class CompleteTodoCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IUserContext _userContext;
    private readonly ITodoRepository _todoRepository;
    private readonly CompleteTodoCommandHandler _handler;

    public CompleteTodoCommandHandlerTests()
    {
        _userContext = Substitute.For<IUserContext>();
        _userContext.UserId.Returns(UserId);

        _todoRepository = Substitute.For<ITodoRepository>();

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);

        _handler = new CompleteTodoCommandHandler(_userContext, _todoRepository, timeProvider);
    }

    [Fact]
    public async Task GivenTheTodoDoesNotExist_WhenHandling_ThenReturnsNotFound()
    {
        _todoRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TodoAggregate?)null);

        var result = await _handler.HandleAsync(new CompleteTodoCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task GivenTheTodoBelongsToAnotherUser_WhenHandling_ThenReturnsBadRequest()
    {
        var todo = TodoAggregate.Create(Guid.NewGuid(), "Title", "Description");
        _todoRepository.GetByIdAsync(todo.Id, Arg.Any<CancellationToken>()).Returns(todo);

        var result = await _handler.HandleAsync(new CompleteTodoCommand(todo.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BadRequest, result.Error.Type);
    }

    [Fact]
    public async Task GivenTheTodoIsAlreadyDeleted_WhenHandling_ThenPropagatesTheDomainError()
    {
        var todo = TodoAggregate.Rehydrate(Guid.NewGuid(), UserId, "Title", "Description", null, DateTime.UtcNow);
        _todoRepository.GetByIdAsync(todo.Id, Arg.Any<CancellationToken>()).Returns(todo);

        var result = await _handler.HandleAsync(new CompleteTodoCommand(todo.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Todo.Complete", result.Error.Code);
        await _todoRepository.DidNotReceive().UpdateAsync(Arg.Any<TodoAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenTheTodoIsOwnedAndNotFinalized_WhenHandling_ThenCompletesTheTodo()
    {
        var todo = TodoAggregate.Create(UserId, "Title", "Description");
        _todoRepository.GetByIdAsync(todo.Id, Arg.Any<CancellationToken>()).Returns(todo);
        _todoRepository
            .UpdateAsync(Arg.Any<TodoAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _handler.HandleAsync(new CompleteTodoCommand(todo.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(todo.CompletedOnUtc);
        await _todoRepository.Received(1).UpdateAsync(todo, Arg.Any<CancellationToken>());
    }
}

