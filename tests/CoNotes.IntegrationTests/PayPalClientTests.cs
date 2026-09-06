using CoNotes.Application.Abstractions;
using CoNotes.Domain.AppUsers;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// 對真的 PayPal Sandbox REST API 打的整合測試——需要在環境變數設定 `PayPal__ClientId`／
/// `PayPal__ClientSecret`(真的 sandbox app 憑證)才能通過, 跟這個專案裡其他需要真的
/// Postgres/Redis/Keycloak 的整合測試是同樣的慣例：外部依賴沒有準備好, 測試就是失敗,
/// 不做靜默略過。
/// </summary>
[Collection(nameof(DatabaseCollection))]
public sealed class PayPalClientTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task GivenValidCredentials_WhenCreatingOrder_ThenReturnsOrderIdAndApprovalUrl()
    {
        using var scope = factory.Services.CreateScope();
        var payPalClient = scope.ServiceProvider.GetRequiredService<IPayPalClient>();

        var order = await payPalClient.CreateOrderAsync(
            PlanTier.Pro,
            "http://localhost:4200/billing/confirm",
            "http://localhost:4200/billing/cancel",
            CancellationToken.None
        );

        Assert.False(string.IsNullOrWhiteSpace(order.OrderId));
        Assert.StartsWith("https://www.sandbox.paypal.com/", order.ApprovalUrl);
    }

    [Fact]
    public async Task GivenOrderThatHasNotBeenApprovedYet_WhenCapturing_ThenReturnsNotCompleted()
    {
        using var scope = factory.Services.CreateScope();
        var payPalClient = scope.ServiceProvider.GetRequiredService<IPayPalClient>();

        var order = await payPalClient.CreateOrderAsync(
            PlanTier.ProMax,
            "http://localhost:4200/billing/confirm",
            "http://localhost:4200/billing/cancel",
            CancellationToken.None
        );

        var captureResult = await payPalClient.CaptureOrderAsync(order.OrderId, CancellationToken.None);

        Assert.False(captureResult.IsCompleted);
    }

    /// <summary>
    /// 需要另外設定環境變數 `PayPal__WebhookId`(真的向 PayPal 註冊過的 webhook id)才會真的呼叫
    /// PayPal 的 verify-webhook-signature API——用捏造的 transmission 資訊送過去, 驗證 PayPal
    /// 真的會回報驗證失敗, 而不是我們自己憑空判斷。
    /// </summary>
    [Fact]
    public async Task GivenForgedWebhookTransmissionData_WhenVerifying_ThenReturnsNull()
    {
        using var scope = factory.Services.CreateScope();
        var payPalClient = scope.ServiceProvider.GetRequiredService<IPayPalClient>();

        var headers = new PayPalWebhookHeaders(
            AuthAlgo: "SHA256withRSA",
            CertUrl: "https://api.sandbox.paypal.com/v1/notifications/certs/not-a-real-cert",
            TransmissionId: Guid.NewGuid().ToString(),
            TransmissionSig: "forged-signature",
            TransmissionTime: DateTime.UtcNow.ToString("O")
        );
        var rawBody = """{"event_type":"PAYMENT.CAPTURE.COMPLETED","resource":{"custom_id":"Pro"}}""";

        var result = await payPalClient.TryVerifyCaptureCompletedEventAsync(headers, rawBody, CancellationToken.None);

        Assert.Null(result);
    }
}
