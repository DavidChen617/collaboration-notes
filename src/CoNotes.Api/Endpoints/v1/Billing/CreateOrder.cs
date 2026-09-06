using CoNotes.Api.Extensions;
using CoNotes.Application.Abstractions;
using CoNotes.Domain.AppUsers;

namespace CoNotes.Api.Endpoints.v1.Billing;

internal sealed class CreateOrderEndpoint : IEndpoint<BillingGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/orders", HandleAsync)
            .WithName("CreatePayPalOrder")
            .WithSummary("建立 PayPal 一次性付款訂單")
            .WithDescription("建立一筆對應指定等級的 PayPal 訂單, 回傳讓使用者導去核准付款的網址")
            .Produces<CreateOrderDto>()
            .ProduceProblem(StatusCodes.Status400BadRequest, "無效的訂閱等級");
    }

    private static async Task<IResult> HandleAsync(
        CreateOrderRequest request,
        IPayPalClient payPalClient,
        CancellationToken ct
    )
    {
        if (!Enum.TryParse<PlanTier>(request.PlanTier, out var planTier) || planTier == PlanTier.Free)
            return Results.BadRequest("無效的訂閱等級, 只能是 Pro 或 ProMax!");

        var order = await payPalClient.CreateOrderAsync(planTier, request.ReturnUrl, request.CancelUrl, ct);

        return Results.Ok(new CreateOrderDto(order.OrderId, order.ApprovalUrl));
    }
}

public sealed record CreateOrderRequest(string PlanTier, string ReturnUrl, string CancelUrl);

public sealed record CreateOrderDto(string OrderId, string ApprovalUrl);
