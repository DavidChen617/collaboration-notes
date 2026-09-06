using CoNotes.Application.AppUsers.EventHandling;
using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Billing.Events;
using Davish.Result;
using NSubstitute;
using AppUserAggregate = CoNotes.Domain.AppUsers.AppUser;

namespace UnitTests.Application.AppUsers.EventHandling;

public class LicenseCodeRedeemedDomainEventHandlerTests
{
    [Fact]
    public async Task GivenLicenseCodeRedeemedEvent_WhenHandled_ThenAppUserPlanTierUpdated()
    {
        var appUserId = Guid.NewGuid();
        var appUser = AppUserAggregate.Rehydrate(appUserId, Guid.NewGuid().ToString(), DateTime.UtcNow, PlanTier.Free);

        var appUserRepository = Substitute.For<IAppUserRepository>();
        appUserRepository.FindByIdAsync(appUserId, Arg.Any<CancellationToken>()).Returns(appUser);
        appUserRepository.UpdateAsync(Arg.Any<AppUserAggregate>(), Arg.Any<CancellationToken>()).Returns(Result.Success());

        var handler = new LicenseCodeRedeemedDomainEventHandler(appUserRepository);
        var domainEvent = new LicenseCodeRedeemedDomainEvent(Guid.NewGuid(), appUserId, PlanTier.ProMax);

        await handler.HandleAsync(domainEvent, CancellationToken.None);

        Assert.Equal(PlanTier.ProMax, appUser.PlanTier);
        await appUserRepository.Received(1).UpdateAsync(appUser, Arg.Any<CancellationToken>());
    }
}
