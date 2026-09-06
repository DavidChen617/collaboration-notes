using CoNotes.Domain.AppUsers;

namespace CoNotes.Application.Abstractions;

/// <summary>
/// PayPal 一次性付款(Orders API)的抽象——不是 Subscriptions API，這裡刻意只處理單筆訂單的
/// 建立與 capture(見 design.md 決定 2)。呼叫者(Api 層)先建立訂單、把使用者導去 <see cref="PayPalOrder.ApprovalUrl"/>
/// 核准，核准後導回時呼叫 <see cref="CaptureOrderAsync"/> 觸發真的扣款；扣款完成後 PayPal 會
/// 呼叫我們的 webhook(見 design.md 決定 2b)，由 <see cref="VerifyWebhookSignatureAsync"/> 驗證
/// 簽章後才真的發 license code——不是 <see cref="CaptureOrderAsync"/> 這裡的回應本身觸發。
/// </summary>
public interface IPayPalClient
{
    Task<PayPalOrder> CreateOrderAsync(PlanTier planTier, string returnUrl, string cancelUrl, CancellationToken ct);
    Task<PayPalCaptureResult> CaptureOrderAsync(string orderId, CancellationToken ct);

    /// <summary>
    /// 驗證 webhook 簽章、確認事件類型是「付款完成」(<c>PAYMENT.CAPTURE.COMPLETED</c>)、並解析出
    /// 對應的訂單與等級。任何一步失敗(簽章無效、事件類型不對、內容解析不出來)都回傳 null——
    /// 呼叫端(Api endpoint)一律回 200 給 PayPal(避免它重送)，但不觸發任何後續動作。
    /// </summary>
    Task<PayPalWebhookEvent?> TryVerifyCaptureCompletedEventAsync(
        PayPalWebhookHeaders headers,
        string rawBody,
        CancellationToken ct
    );
}

public sealed record PayPalOrder(string OrderId, string ApprovalUrl);

public sealed record PayPalCaptureResult(bool IsCompleted, PlanTier PlanTier);

public sealed record PayPalWebhookEvent(string OrderId, PlanTier PlanTier);

public sealed record PayPalWebhookHeaders(
    string AuthAlgo,
    string CertUrl,
    string TransmissionId,
    string TransmissionSig,
    string TransmissionTime
);
