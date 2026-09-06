using CoNotes.Api.Extensions;
using CoNotes.Application.Billing.Commands.Redeem;

namespace CoNotes.Api.Endpoints.v1.Billing;

internal sealed class RedeemLicenseCodeEndpoint : IEndpoint<BillingGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/license-codes/redeem", HandleAsync)
            .WithName("RedeemLicenseCode")
            .WithSummary("兌換 license code")
            .WithDescription("兌換一組尚未使用過的有效 license code, 把目前使用者的訂閱等級更新為該 code 對應的等級")
            .Produces<RedeemLicenseCodeDto>()
            .ProduceProblem(StatusCodes.Status404NotFound, "找不到這組 license code")
            .ProduceProblem(StatusCodes.Status400BadRequest, "這組 license code 已經被兌換過");
    }

    private static async Task<IResult> HandleAsync(
        RedeemLicenseCodeRequest request,
        ISender sender,
        CancellationToken ct
    )
    {
        var result = await sender.SendAsync(new RedeemLicenseCodeCommand(request.Code), ct);

        return result.ToOk();
    }
}

public sealed record RedeemLicenseCodeRequest(string Code);
