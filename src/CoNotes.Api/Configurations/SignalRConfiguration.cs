namespace CoNotes.Api.Configurations;

internal static class SignalRConfiguration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSignalRConfiguration(IConfiguration configuration, IHostEnvironment environment)
        {
            var signalRBuilder = services.AddSignalR(o => o.EnableDetailedErrors = environment.IsDevelopment());

            var redisConnectionString = configuration["Redis:ConnectionString"];

            if (!string.IsNullOrEmpty(redisConnectionString))
                signalRBuilder.AddStackExchangeRedis(redisConnectionString);

            return services;
        }
    }
}
