using CoNotes.Domain.AppUsers;

namespace CoNotes.Application.Abstractions;

/// <summary>
/// PayPal 一次性付款(Orders API)的抽象——不是 Subscriptions API，這裡刻意只處理單筆訂單的
/// 建立與 capture(見 design.md 決定 2)。呼叫者(Api 層)先建立訂單、把使用者導去 <see cref="PayPalOrder.ApprovalUrl"/>
/// 核准，核准後導回時再呼叫 <see cref="CaptureOrderAsync"/> 確認付款是否真的完成。
/// </summary>
public interface IPayPalClient
{
    Task<PayPalOrder> CreateOrderAsync(PlanTier planTier, string returnUrl, string cancelUrl, CancellationToken ct);
    Task<PayPalCaptureResult> CaptureOrderAsync(string orderId, CancellationToken ct);
}

public sealed record PayPalOrder(string OrderId, string ApprovalUrl);

public sealed record PayPalCaptureResult(bool IsCompleted, PlanTier PlanTier);
