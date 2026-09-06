using CoNotes.Api.Extensions;
using CoNotes.Domain.Billing;

namespace CoNotes.Api.Endpoints.v1.Billing;

internal sealed class GetLicenseCodeForOrderEndpoint : IEndpoint<BillingGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/orders/{orderId}/license-code", HandleAsync)
            .WithName("GetLicenseCodeForOrder")
            .WithSummary("查詢某筆訂單是否已經產生 license code")
            .WithDescription(
                "PayPal 的 webhook 是非同步送達的, 前端在導回確認頁時用這個 endpoint 輪詢: " +
                "webhook 處理完、code 已經產生就回 200, 還沒處理完就回 404"
            )
            .Produces<LicenseCodeResponse>()
            .ProduceProblem(StatusCodes.Status404NotFound, "這筆訂單還沒有對應的 license code(webhook 可能還沒送達)");
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] string orderId,
        ILicenseCodeRepository licenseCodeRepository,
        CancellationToken ct
    )
    {
        var licenseCode = await licenseCodeRepository.GetByPayPalOrderIdAsync(orderId, ct);

        return licenseCode is null
            ? Results.NotFound()
            : Results.Ok(new LicenseCodeResponse(licenseCode.Code));
    }
}

public sealed record LicenseCodeResponse(string Code);
