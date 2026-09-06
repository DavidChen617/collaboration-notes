using CoNotes.Domain.AppUsers;

namespace CoNotes.Application.Billing.Commands.Revoke;

internal sealed class RevokeAppUserPlanTierCommandHandler(
    IUserContext userContext,
    IAppUserRepository appUserRepository
) : ICommandHandler<RevokeAppUserPlanTierCommand, Result>
{
    public async Task<Result> HandleAsync(RevokeAppUserPlanTierCommand command, CancellationToken cancellationToken)
    {
        if (!userContext.IsAdmin())
            return new Error("AppUser.RevokePlanTier", "只有系統管理者能撤銷訂閱等級!", ErrorType.BadRequest);

        var appUser = await appUserRepository.FindByIdAsync(command.AppUserId, cancellationToken);

        if (appUser is null)
            return new Error("AppUser.RevokePlanTier", "找不到使用者!", ErrorType.NotFound);

        appUser.RevokePlanTier();

        return await appUserRepository.UpdateAsync(appUser, cancellationToken);
    }
}
