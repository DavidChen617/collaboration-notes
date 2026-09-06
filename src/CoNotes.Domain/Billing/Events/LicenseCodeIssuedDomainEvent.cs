using CoNotes.Domain.AppUsers;

namespace CoNotes.Domain.Billing.Events;

public sealed record LicenseCodeIssuedDomainEvent(Guid LicenseCodeId, PlanTier PlanTier, string PayPalOrderId) : DomainEvent;
