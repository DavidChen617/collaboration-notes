using CoNotes.Application.AppUsers.Commands.Upsert;
using CoNotes.Domain.AppUsers;
using Davish.Result;
using NSubstitute;
using AppUserAggregate = CoNotes.Domain.AppUsers.AppUser;

namespace UnitTests.Application.AppUsers.Commands;

public class UpsertAppUserCommandHandlerTests
{
    [Fact]
    public async Task GivenAppUserDoesNotExist_WhenHandlingUpsertAppUserCommand_ThenCreatesAndPersistsNewAppUser()
    {
        var keycloakSub = Guid.NewGuid().ToString();
        var appUserRepository = Substitute.For<IAppUserRepository>();
        appUserRepository
            .FindByKeycloakSubAsync(keycloakSub, Arg.Any<CancellationToken>())
            .Returns((AppUserAggregate?)null);
        appUserRepository
            .AddAsync(Arg.Any<AppUserAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var handler = new UpsertAppUserCommandHandler(appUserRepository, TimeProvider.System);
        var command = new UpsertAppUserCommand(keycloakSub);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.AppUserId);
        await appUserRepository.Received(1).AddAsync(
            Arg.Is<AppUserAggregate>(u => u.KeycloakSub == keycloakSub),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenAppUserAlreadyExists_WhenHandlingUpsertAppUserCommand_ThenReturnsExistingAppUserWithoutDuplication()
    {
        var keycloakSub = Guid.NewGuid().ToString();
        var existingAppUser = AppUserAggregate.Create(keycloakSub, DateTime.UtcNow);

        var appUserRepository = Substitute.For<IAppUserRepository>();
        appUserRepository
            .FindByKeycloakSubAsync(keycloakSub, Arg.Any<CancellationToken>())
            .Returns(existingAppUser);

        var handler = new UpsertAppUserCommandHandler(appUserRepository, TimeProvider.System);
        var command = new UpsertAppUserCommand(keycloakSub);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existingAppUser.Id, result.Value.AppUserId);
        await appUserRepository.DidNotReceive().AddAsync(Arg.Any<AppUserAggregate>(), Arg.Any<CancellationToken>());
    }
}
