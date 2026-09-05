using Davish.Result;
using NSubstitute;
using Todo.Application.Absctractions;
using Todo.Application.Todos.Commands.Create;
using Todo.Domain.Todos;
using TodoAggregate = Todo.Domain.Todos.Todo;

namespace UnitTests.Application.Todos.Commands;

public class CreateTodoCommandHandlerTests
{
    [Fact]
    public async Task GivenAValidCommand_WhenHandling_ThenCreatesAndPersistsATodoOwnedByTheCurrentUser()
    {
        var userId = Guid.NewGuid();
        var userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);

        var todoRepository = Substitute.For<ITodoRepository>();
        todoRepository
            .AddAsync(Arg.Any<TodoAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var handler = new CreateTodoCommandHandler(userContext, todoRepository);
        var command = new CreateTodoCommand("Buy milk", "2%, not whole");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.TodoId);
        await todoRepository.Received(1).AddAsync(
            Arg.Is<TodoAggregate>(t =>
                t.UserId == userId && t.Title == "Buy milk" && t.Description == "2%, not whole"),
            Arg.Any<CancellationToken>());
    }
}

