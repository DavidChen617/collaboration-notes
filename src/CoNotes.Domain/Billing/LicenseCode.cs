using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Billing.Events;

namespace CoNotes.Domain.Billing;

public sealed class LicenseCode : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public PlanTier PlanTier { get; private set; }
    public string PayPalOrderId { get; private set; } = null!;
    public Guid? RedeemedByAppUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RedeemedAt { get; private set; }

    private LicenseCode(
        Guid id,
        string code,
        PlanTier planTier,
        string payPalOrderId,
        Guid? redeemedByAppUserId,
        DateTime createdAt,
        DateTime? redeemedAt
    )
    {
        Id = id;
        Code = code;
        PlanTier = planTier;
        PayPalOrderId = payPalOrderId;
        RedeemedByAppUserId = redeemedByAppUserId;
        CreatedAt = createdAt;
        RedeemedAt = redeemedAt;
    }

    public static LicenseCode Issue(string code, PlanTier planTier, string payPalOrderId, DateTime nowUtc)
    {
        var licenseCode = new LicenseCode(
            Guid.CreateVersion7(),
            code,
            planTier,
            payPalOrderId,
            redeemedByAppUserId: null,
            createdAt: nowUtc,
            redeemedAt: null
        );

        licenseCode.RaiseDomainEvent(new LicenseCodeIssuedDomainEvent(licenseCode.Id, planTier, payPalOrderId));

        return licenseCode;
    }

    public static LicenseCode Rehydrate(
        Guid id,
        string code,
        PlanTier planTier,
        string payPalOrderId,
        Guid? redeemedByAppUserId,
        DateTime createdAt,
        DateTime? redeemedAt
    )
    {
        return new(id, code, planTier, payPalOrderId, redeemedByAppUserId, createdAt, redeemedAt);
    }

    /// <summary>
    /// 兌換不限制身份, 誰輸入這組尚未使用的 code 都能套用到自己的帳號上(見 design.md 決定 4)。
    /// 「未兌換才能兌換」是這個 Aggregate 自身的不變條件; 併發下兩個請求同時兌換同一組 code
    /// 只能有一個成功, 這個保證由 Infrastructure 層的條件式 UPDATE 負責(見 <c>LicenseCodeRepository</c>),
    /// 這裡表達的是單一請求視角下、根據目前已讀取狀態的邏輯拒絕。
    /// </summary>
    public Result Redeem(Guid appUserId, DateTime nowUtc)
    {
        if (RedeemedByAppUserId is not null)
            return new Error("LicenseCode.Redeem", "這組 license code 已經被兌換過!", ErrorType.BadRequest);

        RedeemedByAppUserId = appUserId;
        RedeemedAt = nowUtc;

        RaiseDomainEvent(new LicenseCodeRedeemedDomainEvent(Id, appUserId, PlanTier));

        return Result.Success();
    }
}
