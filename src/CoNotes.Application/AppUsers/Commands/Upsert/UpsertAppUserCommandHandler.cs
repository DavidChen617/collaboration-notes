using CoNotes.Domain.AppUsers;
using AppUserAggregate = CoNotes.Domain.AppUsers.AppUser;

namespace CoNotes.Application.AppUsers.Commands.Upsert;

internal sealed class UpsertAppUserCommandHandler(
    IAppUserRepository appUserRepository
) : ICommandHandler<UpsertAppUserCommand, Result<UpsertAppUserDto>>
{
    public async Task<Result<UpsertAppUserDto>> HandleAsync(
        UpsertAppUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var existingAppUser = await appUserRepository.FindByKeycloakSubAsync(command.KeycloakSub, cancellationToken);
        if (existingAppUser is not null)
            return new UpsertAppUserDto(existingAppUser.Id);

        var appUser = AppUserAggregate.Create(command.KeycloakSub);

        await appUserRepository.AddAsync(appUser, cancellationToken);

        return new UpsertAppUserDto(appUser.Id);
    }
}
