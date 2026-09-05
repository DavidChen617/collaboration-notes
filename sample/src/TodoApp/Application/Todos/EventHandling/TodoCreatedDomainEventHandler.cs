namespace Todo.Application.Todos.EventHandling;

internal sealed class TodoCreatedDomainEventHandler : IDomainEventHandler<TodoCreatedDomainEvent>
{
    public Task HandleAsync(TodoCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine("Do work here..");
        return Task.CompletedTask;
    }
}

