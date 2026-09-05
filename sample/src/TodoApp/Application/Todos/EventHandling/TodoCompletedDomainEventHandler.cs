namespace Todo.Application.Todos.EventHandling;

internal sealed class TodoCompletedDomainEventHandler : IDomainEventHandler<TodoCompletedDomainEvent>
{
    public Task HandleAsync(TodoCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine("Do work here..");
        return Task.CompletedTask;
    }
}

