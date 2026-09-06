using CoNotes.Domain.Notes;
using CoNotes.Infrastructure.AppUsers;
using CoNotes.Infrastructure.Identity;
using CoNotes.Infrastructure.Notes;
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
            services.AddScoped<INoteEditHistoryStore, NoteEditHistoryStore>();
            services.AddScoped<IUserContext, UserContext>();

            services.AddScoped<IAggregateRootChangeTracker, AggregateRootChangeTracker>();
            services.AddScoped<AppDbContext>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddSingleton(TimeProvider.System);

            return services;
        }
    }
}
