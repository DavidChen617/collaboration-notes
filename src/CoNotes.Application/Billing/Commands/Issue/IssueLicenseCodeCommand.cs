using CoNotes.Domain.AppUsers;

namespace CoNotes.Application.Billing.Commands.Issue;

public sealed record IssueLicenseCodeCommand(PlanTier PlanTier, string PayPalOrderId) : ICommand<Result<IssueLicenseCodeDto>>;

public sealed record IssueLicenseCodeDto(string Code);
