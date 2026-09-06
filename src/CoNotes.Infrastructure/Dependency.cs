using CoNotes.Domain.Billing;
using CoNotes.Domain.ChatMessages;
using CoNotes.Domain.Notes;
using CoNotes.Infrastructure.AppUsers;
using CoNotes.Infrastructure.Billing;
using CoNotes.Infrastructure.Billing.Providers;
using CoNotes.Infrastructure.ChatMessages;
using CoNotes.Infrastructure.ChatMessages.Providers;
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
            services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
            services.AddScoped<ILicenseCodeRepository, LicenseCodeRepository>();
            services.AddSingleton(new HttpClient());
            services.AddSingleton<GroqChatProvider>();
            services.AddSingleton<GeminiChatProvider>();
            services.AddSingleton<IAiChatProvider>(services => services.GetRequiredService<GroqChatProvider>());
            services.AddSingleton<IAiChatProvider>(services => services.GetRequiredService<GeminiChatProvider>());
            services.AddSingleton<IPayPalClient, PayPalClient>();
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
