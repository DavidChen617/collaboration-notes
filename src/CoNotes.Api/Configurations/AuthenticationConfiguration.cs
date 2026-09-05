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
                });

            return services;
        }
    }
}
