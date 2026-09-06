using CoNotes.Application.Billing.Commands.Issue;
using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Billing;
using NSubstitute;

namespace UnitTests.Application.Billing.Commands;

public class IssueLicenseCodeCommandHandlerTests
{
    [Fact]
    public async Task GivenOrderCompletedWebhookEvent_WhenHandled_ThenLicenseCodeIssuedForCorrectPlanTier()
    {
        var licenseCodeRepository = Substitute.For<ILicenseCodeRepository>();
        var handler = new IssueLicenseCodeCommandHandler(licenseCodeRepository, TimeProvider.System);

        var result = await handler.HandleAsync(
            new IssueLicenseCodeCommand(PlanTier.ProMax, "PAYPAL-ORDER-1"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Code));
        await licenseCodeRepository.Received(1).AddAsync(
            Arg.Is<LicenseCode>(code =>
                code.PlanTier == PlanTier.ProMax &&
                code.PayPalOrderId == "PAYPAL-ORDER-1" &&
                code.Code == result.Value.Code
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task GivenOrderAlreadyHasALicenseCode_WhenHandledAgain_ThenReturnsExistingCodeWithoutIssuingANewOne()
    {
        var existingCode = LicenseCode.Issue("EXISTING123", PlanTier.Pro, "PAYPAL-ORDER-1", DateTime.UtcNow);
        var licenseCodeRepository = Substitute.For<ILicenseCodeRepository>();
        licenseCodeRepository
            .GetByPayPalOrderIdAsync("PAYPAL-ORDER-1", Arg.Any<CancellationToken>())
            .Returns(existingCode);
        var handler = new IssueLicenseCodeCommandHandler(licenseCodeRepository, TimeProvider.System);

        var result = await handler.HandleAsync(
            new IssueLicenseCodeCommand(PlanTier.Pro, "PAYPAL-ORDER-1"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("EXISTING123", result.Value.Code);
        await licenseCodeRepository.DidNotReceive().AddAsync(Arg.Any<LicenseCode>(), Arg.Any<CancellationToken>());
    }
}
