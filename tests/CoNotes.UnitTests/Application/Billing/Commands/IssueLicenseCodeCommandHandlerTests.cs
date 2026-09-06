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
}
