using CoNotes.Domain.AppUsers;
using CoNotes.Infrastructure.AppUsers;
using CoNotes.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoNotes.Infrastructure;

public static class Dependency
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfrastructure(IConfiguration configuration)
        {
            services.AddNpgsqlDataSource(configuration.GetConnectionString("DefaultConnection")!);

            services.AddScoped<IDbConnectionFactory, DbConnectionFactory>();
            services.AddScoped<IAppUserRepository, AppUserRepository>();

            return services;
        }
    }
}
