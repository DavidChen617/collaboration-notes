using CoNotes.Application.Abstractions;
using CoNotes.Application.Billing.Commands.Revoke;
using CoNotes.Domain.AppUsers;
using Davish.Result;
using NSubstitute;
using AppUserAggregate = CoNotes.Domain.AppUsers.AppUser;

namespace UnitTests.Application.Billing.Commands;

public class RevokeAppUserPlanTierCommandHandlerTests
{
    [Fact]
    public async Task GivenAdminRevokesUser_WhenCommandHandled_ThenPlanTierSetToFree()
    {
        var targetAppUserId = Guid.NewGuid();
        var appUser = AppUserAggregate.Rehydrate(targetAppUserId, Guid.NewGuid().ToString(), DateTime.UtcNow, PlanTier.ProMax);

        var userContext = Substitute.For<IUserContext>();
        userContext.IsAdmin().Returns(true);

        var appUserRepository = Substitute.For<IAppUserRepository>();
        appUserRepository.FindByIdAsync(targetAppUserId, Arg.Any<CancellationToken>()).Returns(appUser);
        appUserRepository.UpdateAsync(Arg.Any<AppUserAggregate>(), Arg.Any<CancellationToken>()).Returns(Result.Success());

        var handler = new RevokeAppUserPlanTierCommandHandler(userContext, appUserRepository);

        var result = await handler.HandleAsync(new RevokeAppUserPlanTierCommand(targetAppUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanTier.Free, appUser.PlanTier);
        await appUserRepository.Received(1).UpdateAsync(appUser, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenNonAdminAttemptsRevoke_WhenCommandHandled_ThenRejected()
    {
        var targetAppUserId = Guid.NewGuid();

        var userContext = Substitute.For<IUserContext>();
        userContext.IsAdmin().Returns(false);

        var appUserRepository = Substitute.For<IAppUserRepository>();

        var handler = new RevokeAppUserPlanTierCommandHandler(userContext, appUserRepository);

        var result = await handler.HandleAsync(new RevokeAppUserPlanTierCommand(targetAppUserId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        await appUserRepository.DidNotReceive().FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await appUserRepository.DidNotReceive().UpdateAsync(Arg.Any<AppUserAggregate>(), Arg.Any<CancellationToken>());
    }
}
