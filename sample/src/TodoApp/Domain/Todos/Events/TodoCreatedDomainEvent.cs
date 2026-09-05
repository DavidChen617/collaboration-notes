namespace Todo.Domain.Todos.Events;

public sealed record TodoCreatedDomainEvent(Guid TodoId) : DomainEvent;

