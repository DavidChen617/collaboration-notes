using CoNotes.Domain.AppUsers;
using CoNotes.Domain.AppUsers.Events;
using AppUserAggregate = CoNotes.Domain.AppUsers.AppUser;

namespace UnitTests.Domain;

public class AppUserTests
{
    [Fact]
    public void GivenNewKeycloakSub_WhenCreatingAppUser_ThenRaisesAppUserProvisionedEvent()
    {
        var keycloakSub = Guid.NewGuid().ToString();

        var appUser = AppUserAggregate.Create(keycloakSub, DateTime.UtcNow);

        var domainEvent = Assert.Single(appUser.DomainEvents);
        var provisionedEvent = Assert.IsType<AppUserProvisionedDomainEvent>(domainEvent);
        Assert.Equal(appUser.Id, provisionedEvent.AppUserId);
        Assert.Equal(keycloakSub, provisionedEvent.KeycloakSub);
    }

    [Fact]
    public void GivenNewAppUser_WhenCreated_ThenPlanTierDefaultsToFree()
    {
        var appUser = AppUserAggregate.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);

        Assert.Equal(PlanTier.Free, appUser.PlanTier);
    }

    [Fact]
    public void GivenAppUserWithProTier_WhenRevoked_ThenPlanTierSetToFreeAndEventRaised()
    {
        var appUser = AppUserAggregate.Rehydrate(Guid.NewGuid(), Guid.NewGuid().ToString(), DateTime.UtcNow, PlanTier.Pro);

        appUser.RevokePlanTier();

        Assert.Equal(PlanTier.Free, appUser.PlanTier);
        var revokedEvent = Assert.IsType<AppUserPlanTierRevokedDomainEvent>(
            Assert.Single(appUser.DomainEvents, e => e is AppUserPlanTierRevokedDomainEvent)
        );
        Assert.Equal(appUser.Id, revokedEvent.AppUserId);
    }

    [Fact]
    public void GivenLicenseCodeRedeemedEvent_WhenAppliedToAppUser_ThenPlanTierMatchesEventPlanTier()
    {
        var appUser = AppUserAggregate.Create(Guid.NewGuid().ToString(), DateTime.UtcNow);

        appUser.ApplyRedeemedPlanTier(PlanTier.ProMax);

        Assert.Equal(PlanTier.ProMax, appUser.PlanTier);
    }
}
