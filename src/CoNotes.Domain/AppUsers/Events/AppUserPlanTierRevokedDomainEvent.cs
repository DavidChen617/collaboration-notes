namespace CoNotes.Domain.AppUsers.Events;

public sealed record AppUserPlanTierRevokedDomainEvent(Guid AppUserId) : DomainEvent;
