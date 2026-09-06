using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Billing;
using CoNotes.Domain.Billing.Events;

namespace UnitTests.Domain;

public class LicenseCodeTests
{
    [Fact]
    public void GivenUnusedCode_WhenRedeemed_ThenMarkedRedeemedAndRaisesLicenseCodeRedeemed()
    {
        var licenseCode = LicenseCode.Issue("ABC123", PlanTier.Pro, "PAYPAL-ORDER-1", DateTime.UtcNow);
        var appUserId = Guid.NewGuid();
        var redeemedAt = DateTime.UtcNow;

        var result = licenseCode.Redeem(appUserId, redeemedAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(appUserId, licenseCode.RedeemedByAppUserId);
        Assert.Equal(redeemedAt, licenseCode.RedeemedAt);

        var domainEvent = Assert.IsType<LicenseCodeRedeemedDomainEvent>(
            Assert.Single(licenseCode.DomainEvents, e => e is LicenseCodeRedeemedDomainEvent)
        );
        Assert.Equal(licenseCode.Id, domainEvent.LicenseCodeId);
        Assert.Equal(appUserId, domainEvent.AppUserId);
        Assert.Equal(PlanTier.Pro, domainEvent.PlanTier);
    }

    [Fact]
    public void GivenAlreadyRedeemedCode_WhenRedeemAttempted_ThenRejectedAndNoEventRaised()
    {
        var licenseCode = LicenseCode.Issue("ABC123", PlanTier.Pro, "PAYPAL-ORDER-1", DateTime.UtcNow);
        var firstRedeemer = Guid.NewGuid();
        licenseCode.Redeem(firstRedeemer, DateTime.UtcNow);

        var result = licenseCode.Redeem(Guid.NewGuid(), DateTime.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal(firstRedeemer, licenseCode.RedeemedByAppUserId);
        // 只有第一次成功的兌換觸發過事件, 第二次被拒絕的嘗試沒有再多加一個
        Assert.Single(licenseCode.DomainEvents, e => e is LicenseCodeRedeemedDomainEvent);
    }
}
