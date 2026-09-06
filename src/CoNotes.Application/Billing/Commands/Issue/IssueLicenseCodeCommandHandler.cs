using CoNotes.Domain.Billing;

namespace CoNotes.Application.Billing.Commands.Issue;

internal sealed class IssueLicenseCodeCommandHandler(
    ILicenseCodeRepository licenseCodeRepository,
    TimeProvider timeProvider
) : ICommandHandler<IssueLicenseCodeCommand, Result<IssueLicenseCodeDto>>
{
    public async Task<Result<IssueLicenseCodeDto>> HandleAsync(
        IssueLicenseCodeCommand command,
        CancellationToken cancellationToken
    )
    {
        // 產生一個不可猜測的 code 是技術細節, 不是 Domain 該決定的事(比照 ShareLinkToken 的先例):
        // Domain 只接收已經產生好的 code、記錄狀態、觸發事件。
        var code = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        var licenseCode = LicenseCode.Issue(
            code,
            command.PlanTier,
            command.PayPalOrderId,
            timeProvider.GetUtcNow().UtcDateTime
        );

        await licenseCodeRepository.AddAsync(licenseCode, cancellationToken);

        return new IssueLicenseCodeDto(licenseCode.Code);
    }
}
