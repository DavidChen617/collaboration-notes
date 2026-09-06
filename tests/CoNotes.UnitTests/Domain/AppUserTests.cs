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
}
