namespace CoNotes.Api.Configurations;

internal static class CorsConfiguration
{
    public const string PolicyName = "Frontend";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddCorsConfiguration(IConfiguration configuration)
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            services.AddCors(o => o.AddPolicy(PolicyName, policy =>
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()));

            return services;
        }
    }
}
