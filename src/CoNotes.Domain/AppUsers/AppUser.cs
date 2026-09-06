using CoNotes.Domain.AppUsers.Events;

namespace CoNotes.Domain.AppUsers;

public sealed class AppUser : AggregateRoot
{
    public string KeycloakSub { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }

    private AppUser(Guid id, string keycloakSub, DateTime createdAt)
    {
        Id = id;
        KeycloakSub = keycloakSub;
        CreatedOnUtc = createdAt;
    }

    public static AppUser Create(string keycloakSub, DateTime nowUtc)
    {
        var appUser = new AppUser(Guid.CreateVersion7(), keycloakSub, nowUtc);

        appUser.RaiseDomainEvent(new AppUserProvisionedDomainEvent(appUser.Id, keycloakSub));

        return appUser;
    }

    public static AppUser Rehydrate(Guid id, string keycloakSub, DateTime createdAt)
    {
        return new(id, keycloakSub, createdAt);
    }
}
