using CoNotes.Application.Abstractions;
using CoNotes.Application.Billing.Commands.Redeem;
using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Billing;
using NSubstitute;

namespace UnitTests.Application.Billing.Commands;

public class RedeemLicenseCodeCommandHandlerTests
{
    [Fact]
    public async Task GivenValidCode_WhenRedeemCommandHandled_ThenLicenseCodeMarkedRedeemed()
    {
        var appUserId = Guid.NewGuid();
        var licenseCode = LicenseCode.Issue("ABC123", PlanTier.Pro, "PAYPAL-ORDER-1", DateTime.UtcNow);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(appUserId);

        var licenseCodeRepository = Substitute.For<ILicenseCodeRepository>();
        licenseCodeRepository.GetByCodeAsync("ABC123", Arg.Any<CancellationToken>()).Returns(licenseCode);
        licenseCodeRepository
            .TryPersistRedemptionAsync(Arg.Any<LicenseCode>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new RedeemLicenseCodeCommandHandler(userContext, licenseCodeRepository, TimeProvider.System);

        var result = await handler.HandleAsync(new RedeemLicenseCodeCommand("ABC123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanTier.Pro, result.Value.PlanTier);
        await licenseCodeRepository.Received(1).TryPersistRedemptionAsync(
            Arg.Is<LicenseCode>(code => code.RedeemedByAppUserId == appUserId),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task GivenAlreadyRedeemedCode_WhenRedeemCommandHandled_ThenRejected()
    {
        var licenseCode = LicenseCode.Issue("ABC123", PlanTier.Pro, "PAYPAL-ORDER-1", DateTime.UtcNow);
        licenseCode.Redeem(Guid.NewGuid(), DateTime.UtcNow);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var licenseCodeRepository = Substitute.For<ILicenseCodeRepository>();
        licenseCodeRepository.GetByCodeAsync("ABC123", Arg.Any<CancellationToken>()).Returns(licenseCode);

        var handler = new RedeemLicenseCodeCommandHandler(userContext, licenseCodeRepository, TimeProvider.System);

        var result = await handler.HandleAsync(new RedeemLicenseCodeCommand("ABC123"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        await licenseCodeRepository.DidNotReceive().TryPersistRedemptionAsync(
            Arg.Any<LicenseCode>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task GivenConcurrentRedemptionLosesTheRace_WhenRedeemCommandHandled_ThenRejected()
    {
        var licenseCode = LicenseCode.Issue("ABC123", PlanTier.Pro, "PAYPAL-ORDER-1", DateTime.UtcNow);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetAppUserIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var licenseCodeRepository = Substitute.For<ILicenseCodeRepository>();
        licenseCodeRepository.GetByCodeAsync("ABC123", Arg.Any<CancellationToken>()).Returns(licenseCode);
        // 模擬另一個併發請求已經先一步真的把它寫進 DB, 這裡的條件式 UPDATE 影響 0 列
        licenseCodeRepository
            .TryPersistRedemptionAsync(Arg.Any<LicenseCode>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new RedeemLicenseCodeCommandHandler(userContext, licenseCodeRepository, TimeProvider.System);

        var result = await handler.HandleAsync(new RedeemLicenseCodeCommand("ABC123"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("LicenseCode.Redeem", result.Error.Code);
    }
}
