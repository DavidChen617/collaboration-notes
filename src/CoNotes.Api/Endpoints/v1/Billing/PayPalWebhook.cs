using CoNotes.Application.Abstractions;
using CoNotes.Application.Billing.Commands.Issue;

namespace CoNotes.Api.Endpoints.v1.Billing;

internal sealed class PayPalWebhookEndpoint : IEndpoint<BillingGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/webhook", HandleAsync)
            .WithName("PayPalWebhook")
            .WithSummary("接收 PayPal webhook")
            .WithDescription(
                "PayPal 付款完成(PAYMENT.CAPTURE.COMPLETED)時呼叫的 webhook, 驗證簽章後才觸發 " +
                "IssueLicenseCodeCommand; 簽章驗證失敗、事件類型不是我們關心的、或內容解析不出來, " +
                "一律回 200(避免 PayPal 重送), 但不做任何事"
            )
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(HttpRequest request, IPayPalClient payPalClient, ISender sender, CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        var headers = new PayPalWebhookHeaders(
            request.Headers["PAYPAL-AUTH-ALGO"].ToString(),
            request.Headers["PAYPAL-CERT-URL"].ToString(),
            request.Headers["PAYPAL-TRANSMISSION-ID"].ToString(),
            request.Headers["PAYPAL-TRANSMISSION-SIG"].ToString(),
            request.Headers["PAYPAL-TRANSMISSION-TIME"].ToString()
        );

        var webhookEvent = await payPalClient.TryVerifyCaptureCompletedEventAsync(headers, rawBody, ct);
        if (webhookEvent is null)
            return Results.Ok();

        await sender.SendAsync(new IssueLicenseCodeCommand(webhookEvent.PlanTier, webhookEvent.OrderId), ct);

        return Results.Ok();
    }
}
