namespace Todo.Domain.Todos.Events;

public sealed record TodoDeletedDomainEvent(Guid TodoId) : DomainEvent;

