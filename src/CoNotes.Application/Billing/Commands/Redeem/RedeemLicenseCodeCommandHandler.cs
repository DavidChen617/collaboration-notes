using CoNotes.Domain.Billing;

namespace CoNotes.Application.Billing.Commands.Redeem;

internal sealed class RedeemLicenseCodeCommandHandler(
    IUserContext userContext,
    ILicenseCodeRepository licenseCodeRepository,
    TimeProvider timeProvider
) : ICommandHandler<RedeemLicenseCodeCommand, Result<RedeemLicenseCodeDto>>
{
    public async Task<Result<RedeemLicenseCodeDto>> HandleAsync(
        RedeemLicenseCodeCommand command,
        CancellationToken cancellationToken
    )
    {
        var licenseCode = await licenseCodeRepository.GetByCodeAsync(command.Code, cancellationToken);

        if (licenseCode is null)
            return new Error("LicenseCode.Redeem", "找不到這組 license code!", ErrorType.NotFound);

        var appUserId = await userContext.GetAppUserIdAsync(cancellationToken);

        var redeemResult = licenseCode.Redeem(appUserId, timeProvider.GetUtcNow().UtcDateTime);
        if (!redeemResult.IsSuccess)
            return redeemResult.Error;

        // 就算上面讀到的狀態是「尚未兌換」, 兩個併發請求仍可能都走到這裡——真正的原子判斷
        // 在 Infrastructure 層的條件式 UPDATE, 這裡再檢查一次持久化是否真的成功。
        var persisted = await licenseCodeRepository.TryPersistRedemptionAsync(licenseCode, cancellationToken);
        if (!persisted)
            return new Error("LicenseCode.Redeem", "這組 license code 已經被兌換過!", ErrorType.BadRequest);

        return new RedeemLicenseCodeDto(licenseCode.PlanTier);
    }
}
