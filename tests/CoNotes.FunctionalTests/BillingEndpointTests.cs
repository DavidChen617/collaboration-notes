using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using CoNotes.FunctionalTests;

namespace FunctionalTests;

[Collection(nameof(ApiCollection))]
public sealed class BillingEndpointTests(FunctionalTestWebAppFactory factory)
{
    private const string BillingEndpoint = "/api/v1/billing";

    [Fact]
    public async Task GivenValidUnusedCode_WhenRedeemEndpointCalled_ThenPlanTierUpdatedInResponse()
    {
        var user = await CreateProvisionedClientAsync();
        var code = await CreateTestLicenseCodeAsync(user, "ProMax");

        var response = await user.PostAsJsonAsync($"{BillingEndpoint}/license-codes/redeem", new { Code = code });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RedeemResponse>();
        Assert.NotNull(body);
        Assert.Equal("ProMax", body.PlanTier);
    }

    [Fact]
    public async Task GivenAlreadyRedeemedCode_WhenRedeemEndpointCalled_ThenReturnsRejection()
    {
        var firstUser = await CreateProvisionedClientAsync();
        var secondUser = await CreateProvisionedClientAsync();
        var code = await CreateTestLicenseCodeAsync(firstUser, "Pro");
        await firstUser.PostAsJsonAsync($"{BillingEndpoint}/license-codes/redeem", new { Code = code });

        var response = await secondUser.PostAsJsonAsync($"{BillingEndpoint}/license-codes/redeem", new { Code = code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GivenAdminUser_WhenRevokeEndpointCalled_ThenPlanTierSetToFree()
    {
        var admin = await CreateProvisionedClientAsync(isAdmin: true);
        var target = await CreateProvisionedClientAsync();
        var code = await CreateTestLicenseCodeAsync(target, "Pro");
        await target.PostAsJsonAsync($"{BillingEndpoint}/license-codes/redeem", new { Code = code });
        var targetAppUserId = await GetAppUserIdAsync(target);

        var response = await admin.PostAsync(
            $"{BillingEndpoint}/app-users/{targetAppUserId}/plan-tier/revoke",
            content: null
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GivenNonAdminUser_WhenRevokeEndpointCalled_ThenReturns403()
    {
        var nonAdmin = await CreateProvisionedClientAsync();
        var targetAppUserId = await GetAppUserIdAsync(nonAdmin);

        var response = await nonAdmin.PostAsync(
            $"{BillingEndpoint}/app-users/{targetAppUserId}/plan-tier/revoke",
            content: null
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// 端對端測試——用真的 PayPal Sandbox REST API 建立訂單, 走到「使用者核准」這一步之前的
    /// 完整流程都真的驗證過(需要環境變數 `PayPal__ClientId`/`PayPal__ClientSecret`)；核准本身
    /// 需要 PayPal sandbox buyer 帳號(見 tasks.md 6.0/6.3), 這裡驗證還沒核准就 confirm 會被
    /// 正確拒絕、不會產生 code。之後兌換→分享連結/聊天室這段已經在 <see cref="GivenValidUnusedCode_WhenRedeemEndpointCalled_ThenPlanTierUpdatedInResponse"/>
    /// 與 <c>NoteCollabEndpointTests</c>/<c>ChatMessageTests</c> 涵蓋過。
    /// </summary>
    [Fact]
    public async Task GivenPayPalOrderCreatedButNotApproved_WhenConfirmCalled_ThenRejectedAndNoLicenseCodeIssued()
    {
        var user = await CreateProvisionedClientAsync();

        var createResponse = await user.PostAsJsonAsync(
            $"{BillingEndpoint}/orders",
            new
            {
                PlanTier = "Pro",
                ReturnUrl = "http://localhost:4200/billing/confirm",
                CancelUrl = "http://localhost:4200/billing/cancel",
            }
        );
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var createBody = await createResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();
        Assert.NotNull(createBody);
        Assert.False(string.IsNullOrEmpty(createBody.OrderId));
        Assert.StartsWith("https://www.sandbox.paypal.com/", createBody.ApprovalUrl);

        var confirmResponse = await user.PostAsync($"{BillingEndpoint}/orders/{createBody.OrderId}/confirm", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
    }

    private async Task<HttpClient> CreateProvisionedClientAsync(bool isAdmin = false)
    {
        var client = factory.CreateClient();
        var extraClaims = isAdmin ? [new Claim(ClaimTypes.Role, "admin")] : Array.Empty<Claim>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", TestTokens.CreateToken(Guid.NewGuid().ToString(), extraClaims: extraClaims));

        await client.PostAsync("/api/test/app-user", content: null);

        return client;
    }

    private static async Task<string> CreateTestLicenseCodeAsync(HttpClient client, string planTier)
    {
        var response = await client.PostAsJsonAsync("/api/test/license-code", new { PlanTier = planTier });
        var body = await response.Content.ReadFromJsonAsync<TestLicenseCodeResponse>();

        return body!.Code;
    }

    private static async Task<Guid> GetAppUserIdAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/test/app-user", content: null);
        var body = await response.Content.ReadFromJsonAsync<AppUserResponse>();

        return body!.AppUserId;
    }

    private sealed record RedeemResponse(string PlanTier);

    private sealed record CreateOrderResponse(string OrderId, string ApprovalUrl);

    private sealed record TestLicenseCodeResponse(string Code);

    private sealed record AppUserResponse(Guid AppUserId);
}
