using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FunctionalTests;

public sealed class FunctionalTestWebAppFactory : WebApplicationFactory<Program>
{
    public const string TestIssuer = "https://auth.test/realms/conotes";
    public const string TestAudience = "conotes-spa";

    public static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("functional-tests-signing-key-do-not-use-elsewhere"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Host=localhost;Database=app;Username=conotes_app;Password=app_password");

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TestIssuer,
                    ValidateAudience = true,
                    ValidAudience = TestAudience,
                    ValidateLifetime = true,
                    IssuerSigningKey = SigningKey,
                };
            });
        });
    }
}
