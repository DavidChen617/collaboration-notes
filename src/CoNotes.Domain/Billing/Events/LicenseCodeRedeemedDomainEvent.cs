using CoNotes.Domain.AppUsers;

namespace CoNotes.Domain.Billing.Events;

/// <summary>
/// 跨 Context 事件——Identity context 訂閱這個事件, 把兌換者的 <c>AppUser.PlanTier</c>
/// 更新成這裡帶的等級(見 <see cref="AppUser.ApplyRedeemedPlanTier"/>)。
/// </summary>
public sealed record LicenseCodeRedeemedDomainEvent(Guid LicenseCodeId, Guid AppUserId, PlanTier PlanTier) : DomainEvent;
