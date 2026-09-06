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
                        },
                    };
                });

            return services;
        }
    }
}
