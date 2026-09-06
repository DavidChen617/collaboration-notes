using CoNotes.Api.Extensions;
using CoNotes.Application.Abstractions;

namespace CoNotes.Api.Endpoints.v1.Billing;

internal sealed class ConfirmOrderEndpoint : IEndpoint<BillingGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/orders/{orderId}/confirm", HandleAsync)
            .WithName("ConfirmPayPalOrder")
            .WithSummary("觸發 PayPal 付款 capture")
            .WithDescription(
                "使用者從 PayPal 核准頁導回後呼叫, 後端呼叫 PayPal 的 Capture API 觸發真的扣款; " +
                "扣款完成後 license code 由 PayPal 送來的 webhook 觸發產生(見 /orders/{orderId}/license-code 輪詢), " +
                "這裡只負責觸發 capture 本身, 不直接產生 code"
            )
            .ProduceProblem(StatusCodes.Status400BadRequest, "PayPal 付款尚未完成");
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] string orderId,
        IPayPalClient payPalClient,
        CancellationToken ct
    )
    {
        var captureResult = await payPalClient.CaptureOrderAsync(orderId, ct);

        return captureResult.IsCompleted
            ? Results.Accepted()
            : Results.BadRequest("PayPal 付款尚未完成, 請重新走一次購買流程!");
    }
}
