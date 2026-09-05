namespace Todo.Api.Configurations;

internal static class AuthenticationConfiguration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAuthenticationConfiguration()
        {
            services.AddAuthorization();
            services.AddAuthentication("Bearer").AddJwtBearer();

            return services;
        }
    }
}

