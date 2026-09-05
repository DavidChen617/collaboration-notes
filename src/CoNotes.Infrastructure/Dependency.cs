using CoNotes.Domain.AppUsers;
using CoNotes.Domain.Notes;
using CoNotes.Infrastructure.AppUsers;
using CoNotes.Infrastructure.Notes;
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
            services.AddScoped<INoteRepository, NoteRepository>();
            services.AddScoped<IUserContext, UserContext>();

            return services;
        }
    }
}
