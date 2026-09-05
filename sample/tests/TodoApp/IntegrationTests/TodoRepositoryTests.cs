using BuildBlock;
using Microsoft.Extensions.DependencyInjection;
using Todo.Domain.Todos;
using TodoAggregate = Todo.Domain.Todos.Todo;

namespace IntegrationTests;

[Collection(nameof(DatabaseCollection))]
public sealed class TodoRepositoryTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task AddAsync_then_GetByIdAsync_returns_the_persisted_todo()
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITodoRepository>();

        var todo = TodoAggregate.Create(Guid.NewGuid(), "Buy milk", "2%, not whole");
        await repository.AddAsync(todo, CancellationToken.None);

        var loaded = await repository.GetByIdAsync(todo.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(todo.UserId, loaded.UserId);
        Assert.Equal(todo.Title, loaded.Title);
        Assert.Equal(todo.Description, loaded.Description);
        Assert.Null(loaded.CompletedOnUtc);
        Assert.Null(loaded.DeletedOnUtc);
    }
}

