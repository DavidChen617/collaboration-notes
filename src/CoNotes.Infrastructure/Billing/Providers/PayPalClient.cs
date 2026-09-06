using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace CoNotes.Infrastructure.Billing.Providers;

/// <summary>
/// PayPal Orders API(v2)的一次性付款(不是 Subscriptions API,見 design.md 決定 2)。
/// 每次呼叫都重新用 client-credentials 換一次 access token——這個專案量體小,
/// 不特別做 token 快取。
/// </summary>
internal sealed class PayPalClient(HttpClient httpClient, IConfiguration configuration) : IPayPalClient
{
    public async Task<PayPalOrder> CreateOrderAsync(
        PlanTier planTier,
        string returnUrl,
        string cancelUrl,
        CancellationToken ct
    )
    {
        var accessToken = await GetAccessTokenAsync(ct);
        var baseUrl = GetBaseUrl();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2/checkout/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    custom_id = planTier.ToString(),
                    amount = new
                    {
                        currency_code = "USD",
                        value = GetPriceUsd(planTier),
                    },
                },
            },
            application_context = new { return_url = returnUrl, cancel_url = cancelUrl },
        });

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var order = await response.Content.ReadFromJsonAsync<CreateOrderResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("PayPal create order 回應為空。");
        var approvalUrl = order.Links?.FirstOrDefault(link => link.Rel == "approve")?.Href
            ?? throw new InvalidOperationException("PayPal create order 回應沒有 approve 連結。");

        return new PayPalOrder(order.Id, approvalUrl);
    }

    public async Task<PayPalCaptureResult> CaptureOrderAsync(string orderId, CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(ct);
        var baseUrl = GetBaseUrl();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/v2/checkout/orders/{orderId}/capture"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, ct);

        // 使用者還沒核准、訂單已經 capture 過、或其他任何非成功狀態, 一律視為「這次沒有真的
        // 完成付款」, 不拋例外——呼叫端(Api endpoint)決定要怎麼回應使用者。
        if (!response.IsSuccessStatusCode)
            return new PayPalCaptureResult(IsCompleted: false, PlanTier.Free);

        var capture = await response.Content.ReadFromJsonAsync<CaptureOrderResponse>(cancellationToken: ct);
        var customId = capture?.PurchaseUnits?.FirstOrDefault()?.Payments?.Captures?.FirstOrDefault()?.CustomId;

        if (capture?.Status != "COMPLETED" || customId is null || !Enum.TryParse<PlanTier>(customId, out var planTier))
            return new PayPalCaptureResult(IsCompleted: false, PlanTier.Free);

        return new PayPalCaptureResult(IsCompleted: true, planTier);
    }

    public async Task<PayPalWebhookEvent?> TryVerifyCaptureCompletedEventAsync(
        PayPalWebhookHeaders headers,
        string rawBody,
        CancellationToken ct
    )
    {
        var webhookId = configuration["PayPal:WebhookId"];
        if (string.IsNullOrWhiteSpace(webhookId))
            throw new InvalidOperationException(
                "PayPal:WebhookId 沒有設定, 無法驗證 webhook 簽章。這是設定缺失, 跟「簽章驗證失敗」是不同情況, 刻意不當成 null 靜默吞掉。"
            );

        using var webhookEventDocument = JsonDocument.Parse(rawBody);
        var accessToken = await GetAccessTokenAsync(ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{GetBaseUrl()}/v1/notifications/verify-webhook-signature"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            auth_algo = headers.AuthAlgo,
            cert_url = headers.CertUrl,
            transmission_id = headers.TransmissionId,
            transmission_sig = headers.TransmissionSig,
            transmission_time = headers.TransmissionTime,
            webhook_id = webhookId,
            webhook_event = webhookEventDocument.RootElement,
        });

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var verification = await response.Content.ReadFromJsonAsync<VerifySignatureResponse>(cancellationToken: ct);
        if (verification?.VerificationStatus != "SUCCESS")
            return null;

        var root = webhookEventDocument.RootElement;
        if (root.GetProperty("event_type").GetString() != "PAYMENT.CAPTURE.COMPLETED")
            return null;

        var resource = root.GetProperty("resource");
        var customId = resource.TryGetProperty("custom_id", out var customIdElement) ? customIdElement.GetString() : null;
        var orderId = resource
            .GetProperty("supplementary_data")
            .GetProperty("related_ids")
            .GetProperty("order_id")
            .GetString();

        if (customId is null || orderId is null || !Enum.TryParse<PlanTier>(customId, out var planTier))
            return null;

        return new PayPalWebhookEvent(orderId, planTier);
    }

    private sealed record VerifySignatureResponse(
        [property: JsonPropertyName("verification_status")] string? VerificationStatus
    );

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var clientId = configuration["PayPal:ClientId"];
        var clientSecret = configuration["PayPal:ClientSecret"];

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GetBaseUrl()}/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"))
        );
        request.Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")]);

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken: ct);

        return token?.AccessToken ?? throw new InvalidOperationException("PayPal 沒有回傳 access token。");
    }

    private string GetBaseUrl() =>
        configuration["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";

    private static string GetPriceUsd(PlanTier planTier) => planTier switch
    {
        PlanTier.Pro => "5.00",
        PlanTier.ProMax => "10.00",
        _ => throw new ArgumentOutOfRangeException(nameof(planTier), planTier, "Free 等級不能購買。"),
    };

    private sealed record AccessTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken
    );

    private sealed record CreateOrderResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("links")] IReadOnlyList<OrderLink>? Links
    );

    private sealed record OrderLink(
        [property: JsonPropertyName("rel")] string Rel,
        [property: JsonPropertyName("href")] string Href
    );

    private sealed record CaptureOrderResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("purchase_units")] IReadOnlyList<CapturePurchaseUnit>? PurchaseUnits
    );

    private sealed record CapturePurchaseUnit(
        [property: JsonPropertyName("payments")] CapturePayments? Payments
    );

    private sealed record CapturePayments(
        [property: JsonPropertyName("captures")] IReadOnlyList<Capture>? Captures
    );

    private sealed record Capture(
        [property: JsonPropertyName("custom_id")] string? CustomId
    );
}
