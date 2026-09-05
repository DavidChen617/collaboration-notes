using CoNotes.Domain.AppUsers;
using Microsoft.Extensions.DependencyInjection;
using AppUserAggregate = CoNotes.Domain.AppUsers.AppUser;

namespace IntegrationTests;

[Collection(nameof(DatabaseCollection))]
public sealed class AppUserRepositoryTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task GivenPostgresDatabase_WhenUpsertingSameKeycloakSubTwice_ThenSecondCallReturnsSameAppUserRow()
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();

        var keycloakSub = Guid.NewGuid().ToString();

        var firstLookup = await repository.FindByKeycloakSubAsync(keycloakSub, CancellationToken.None);
        Assert.Null(firstLookup);

        var created = AppUserAggregate.Create(keycloakSub);
        await repository.AddAsync(created, CancellationToken.None);

        var secondLookup = await repository.FindByKeycloakSubAsync(keycloakSub, CancellationToken.None);

        Assert.NotNull(secondLookup);
        Assert.Equal(created.Id, secondLookup.Id);
        Assert.Equal(keycloakSub, secondLookup.KeycloakSub);
    }
}
