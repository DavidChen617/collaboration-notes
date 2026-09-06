using CoNotes.Domain.AppUsers.Events;

namespace CoNotes.Domain.AppUsers;

public sealed class AppUser : AggregateRoot
{
    public string KeycloakSub { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }
    public PlanTier PlanTier { get; private set; }

    private AppUser(Guid id, string keycloakSub, DateTime createdAt, PlanTier planTier)
    {
        Id = id;
        KeycloakSub = keycloakSub;
        CreatedOnUtc = createdAt;
        PlanTier = planTier;
    }

    public static AppUser Create(string keycloakSub, DateTime nowUtc)
    {
        var appUser = new AppUser(Guid.CreateVersion7(), keycloakSub, nowUtc, PlanTier.Free);

        appUser.RaiseDomainEvent(new AppUserProvisionedDomainEvent(appUser.Id, keycloakSub));

        return appUser;
    }

    public static AppUser Rehydrate(Guid id, string keycloakSub, DateTime createdAt, PlanTier planTier)
    {
        return new(id, keycloakSub, createdAt, planTier);
    }

    /// <summary>
    /// 套用兌換 license code 後解鎖的等級。這是被動接受 Billing context 的
    /// <c>LicenseCodeRedeemed</c> 事件驅動的狀態變更, 兌換本身已經由那個事件表達過了,
    /// 這裡不再另外觸發一個 Domain Event。
    /// </summary>
    public void ApplyRedeemedPlanTier(PlanTier planTier)
    {
        PlanTier = planTier;
    }

    /// <summary>
    /// 系統管理者手動把等級撤銷回 Free。等級不會隨 PayPal 付款後續狀態自動變動,
    /// 這是唯一的收回手段, 純粹發生在 Identity context 內部, 不需要跨 Context 事件。
    /// </summary>
    public void RevokePlanTier()
    {
        PlanTier = PlanTier.Free;

        RaiseDomainEvent(new AppUserPlanTierRevokedDomainEvent(Id));
    }
}
