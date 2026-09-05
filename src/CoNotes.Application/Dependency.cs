using CoNotes.Application.AppUsers.Commands.Upsert;
using Microsoft.Extensions.DependencyInjection;

namespace CoNotes.Application;

public static class Dependency
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            services.AddSendr();

            services.AddRequestHandler<UpsertAppUserCommand, Result<UpsertAppUserDto>, UpsertAppUserCommandHandler>();

            return services;
        }
    }
}
