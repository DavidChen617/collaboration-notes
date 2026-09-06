using CoNotes.Api.Extensions;
using CoNotes.Application.Abstractions;
using CoNotes.Application.Billing.Commands.Issue;

namespace CoNotes.Api.Endpoints.v1.Billing;

internal sealed class ConfirmOrderEndpoint : IEndpoint<BillingGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/orders/{orderId}/confirm", HandleAsync)
            .WithName("ConfirmPayPalOrder")
            .WithSummary("確認 PayPal 付款完成")
            .WithDescription(
                "使用者從 PayPal 核准頁導回後呼叫, 後端呼叫 PayPal 的 Capture API 確認付款狀態; " +
                "確認完成才會產生對應等級的 license code, 沒有真的完成付款則回覆失敗"
            )
            .Produces<IssueLicenseCodeDto>()
            .ProduceProblem(StatusCodes.Status400BadRequest, "PayPal 付款尚未完成");
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] string orderId,
        IPayPalClient payPalClient,
        ISender sender,
        CancellationToken ct
    )
    {
        var captureResult = await payPalClient.CaptureOrderAsync(orderId, ct);

        if (!captureResult.IsCompleted)
            return Results.BadRequest("PayPal 付款尚未完成, 請重新走一次付款流程!");

        var result = await sender.SendAsync(
            new IssueLicenseCodeCommand(captureResult.PlanTier, orderId),
            ct
        );

        return result.ToOk();
    }
}
