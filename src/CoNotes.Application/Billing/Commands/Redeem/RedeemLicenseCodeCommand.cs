using CoNotes.Domain.AppUsers;

namespace CoNotes.Application.Billing.Commands.Redeem;

public sealed record RedeemLicenseCodeCommand(string Code) : ICommand<Result<RedeemLicenseCodeDto>>;

public sealed record RedeemLicenseCodeDto(PlanTier PlanTier);
