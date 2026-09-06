using CoNotes.Api.Extensions;
using CoNotes.Application.Billing.Commands.Revoke;

namespace CoNotes.Api.Endpoints.v1.Billing;

internal sealed class RevokePlanTierEndpoint : IEndpoint<BillingGroupEndpoint>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/app-users/{appUserId}/plan-tier/revoke", HandleAsync)
            .WithName("RevokeAppUserPlanTier")
            .WithSummary("撤銷使用者的訂閱等級")
            .WithDescription("系統管理者專用:把指定使用者的訂閱等級手動撤銷回 Free")
            .RequireAuthorization(policy => policy.RequireRole("admin"))
            .ProduceProblem(StatusCodes.Status404NotFound, "找不到使用者");
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] Guid appUserId,
        ISender sender,
        CancellationToken ct
    )
    {
        var result = await sender.SendAsync(new RevokeAppUserPlanTierCommand(appUserId), ct);

        return result.ToNoContent();
    }
}
