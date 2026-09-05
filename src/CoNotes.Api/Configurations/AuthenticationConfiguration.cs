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
                    // Local dev commonly runs Keycloak over plain HTTP; production Authority
                    // is HTTPS (behind nginx/Cloudflare), so only relax this in Development.
                    options.RequireHttpsMetadata = !environment.IsDevelopment();

                    options.Events = new JwtBearerEvents
                    {
                        // The identity/authentication spec requires an AppUser to exist after the
                        // *first* successful call to any protected endpoint, not just one specific
                        // endpoint - this event fires for every request whose token validates, so
                        // it's the one place to provision it uniformly. Idempotent: UpsertAppUserCommand
                        // is a no-op once the AppUser already exists.
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
