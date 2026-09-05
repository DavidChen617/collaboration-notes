namespace Todo.Application.Todos.EventHandling;

internal sealed class TodoDeletedDomainEventHandler : IDomainEventHandler<TodoDeletedDomainEvent>
{
    public Task HandleAsync(TodoDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine("Do work here..");
        return Task.CompletedTask;
    }
}

