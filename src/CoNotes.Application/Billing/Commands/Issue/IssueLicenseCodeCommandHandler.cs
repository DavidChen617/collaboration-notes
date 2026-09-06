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
        // PayPal 的 webhook 可能因為逾時/網路問題重送同一個事件——這裡靠 PayPalOrderId 做
        // idempotency:同一筆訂單已經發過 code 就直接回傳原本那組, 不要重複發。
        var existing = await licenseCodeRepository.GetByPayPalOrderIdAsync(command.PayPalOrderId, cancellationToken);
        if (existing is not null)
            return new IssueLicenseCodeDto(existing.Code);

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
