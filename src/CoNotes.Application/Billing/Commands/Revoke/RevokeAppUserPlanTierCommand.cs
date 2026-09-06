namespace CoNotes.Application.Billing.Commands.Revoke;

public sealed record RevokeAppUserPlanTierCommand(Guid AppUserId) : ICommand<Result>;
