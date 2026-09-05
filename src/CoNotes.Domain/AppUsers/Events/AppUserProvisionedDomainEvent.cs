namespace CoNotes.Domain.AppUsers.Events;

public sealed record AppUserProvisionedDomainEvent(Guid AppUserId, string KeycloakSub) : DomainEvent;
