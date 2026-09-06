using System.Security.Claims;
using System.Text.Json;
using CoNotes.Application.AppUsers.Commands.Upsert;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CoNotes.Api.Configurations;

internal static class AuthenticationConfiguration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAuthenticationConfiguration(IConfiguration configuration, IHostEnvironment environment)
        {
            services.AddAuthorization();
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = configuration["Authentication:Authority"];
                    options.Audience = configuration["Authentication:Audience"];
                    options.MapInboundClaims = false;
                    // 本機開發通常用 plain HTTP 跑 Keycloak; production 的 Authority 是 HTTPS
                    // (在 nginx/Cloudflare 後面), 所以只在 Development 放寬這項設定。
                    options.RequireHttpsMetadata = !environment.IsDevelopment();

                    options.Events = new JwtBearerEvents
                    {
                        // identity/authentication 的 spec 要求 AppUser 必須在「第一次」成功呼叫任何
                        // 受保護的 endpoint 後就存在, 而不只是某個特定的 endpoint - 這個 event 會在每個
                        // token 驗證通過的 request 觸發, 所以是統一 provision 的唯一位置。
                        // 具備 idempotent 特性: AppUser 已存在時 UpsertAppUserCommand 就是 no-op。
                        OnTokenValidated = async context =>
                        {
                            var keycloakSub = context.Principal?.FindFirst("sub")?.Value;
                            if (keycloakSub is null)
                            {
                                context.Fail("Token has no 'sub' claim.");
                                return;
                            }

                            var sender = context.HttpContext.RequestServices.GetRequiredService<ISender>();
                            await sender.SendAsync(new UpsertAppUserCommand(keycloakSub), context.HttpContext.RequestAborted);

                            AddRealmRoleClaims(context.Principal!);
                        },
                        // 瀏覽器的 WebSocket 交握沒辦法帶自訂的 Authorization header, 所以 SignalR 連線
                        // 改用 query string 的 access_token 帶 JWT; 只在打 /hubs 底下的路徑時才這樣讀,
                        // 其他一般 REST API 呼叫還是照舊只認 Authorization header。
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];

                            if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                                context.Token = accessToken;

                            return Task.CompletedTask;
                        },
                    };
                });

            return services;
        }
    }

    /// <summary>
    /// Keycloak 把 realm role 放在 <c>realm_access</c> 這個claim 裡, 值是一段 JSON(<c>{"roles":[...]}</c>),
    /// 不是 ASP.NET Core 角色驗證(<see cref="ClaimsPrincipal.IsInRole"/>／<c>RequireRole</c>)看的那種
    /// 一個角色一個 claim 的格式。這裡把它攤平成一般的 <see cref="ClaimTypes.Role"/> claim,
    /// 系統管理者的 endpoint 才能直接用 <c>RequireRole("admin")</c> 檢查, 不用另外自己解析 JSON。
    /// </summary>
    private static void AddRealmRoleClaims(ClaimsPrincipal principal)
    {
        var realmAccessJson = principal.FindFirst("realm_access")?.Value;
        if (realmAccessJson is null)
            return;

        using var document = JsonDocument.Parse(realmAccessJson);
        if (!document.RootElement.TryGetProperty("roles", out var rolesElement))
            return;

        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var role in rolesElement.EnumerateArray())
        {
            var roleName = role.GetString();
            if (roleName is not null)
                identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
        }
    }
}
