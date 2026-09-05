namespace Todo.Domain.Todos.Events;

public sealed record TodoCompletedDomainEvent(Guid TodoId) : DomainEvent;

