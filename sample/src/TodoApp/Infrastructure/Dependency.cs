using Todo.Domain.Todos;
using Todo.Infrastructure.Todos;

namespace Todo.Infrastructure;

public static class Dependency
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfrastructure(IConfiguration configuration)
        {
            services.AddNpgsqlDataSource(configuration.GetConnectionString("DefaultConnection")!);

            services.AddScoped<IUserContext, UserContext>();
            services.AddScoped<IDbConnectionFactory, DbConnectionFactory>();
            services.AddScoped<ITodoRepository, TodoRepository>();
            services.AddSingleton(TimeProvider.System);

            services.AddScoped<IAggregateRootChangeTracker, AggregateRootChangeTracker>();
            services.AddScoped<AppDbContext>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}

