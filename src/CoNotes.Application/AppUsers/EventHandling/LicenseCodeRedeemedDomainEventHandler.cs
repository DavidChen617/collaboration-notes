using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Billing.Events;
using Davish.SharedKernel;

namespace CoNotes.Application.AppUsers.EventHandling;

/// <summary>
/// 訂閱 Billing context 發出的跨 Context 事件, 把兌換者的 <c>AppUser.PlanTier</c>
/// 更新成事件帶的等級——這是「一次性解鎖、與 PayPal 後續狀態脫鉤」能夠乾淨落地的關鍵,
/// 兌換的當下是唯一會觸發這個更新的時機點。
/// </summary>
internal sealed class LicenseCodeRedeemedDomainEventHandler(
    IAppUserRepository appUserRepository
) : IDomainEventHandler<LicenseCodeRedeemedDomainEvent>
{
    public async Task HandleAsync(LicenseCodeRedeemedDomainEvent notification, CancellationToken cancellationToken)
    {
        var appUser = await appUserRepository.FindByIdAsync(notification.AppUserId, cancellationToken)
            ?? throw new InvalidOperationException($"No AppUser found for id '{notification.AppUserId}'.");

        appUser.ApplyRedeemedPlanTier(notification.PlanTier);

        await appUserRepository.UpdateAsync(appUser, cancellationToken);
    }
}
