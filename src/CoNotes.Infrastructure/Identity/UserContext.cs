using Microsoft.AspNetCore.Http;

namespace CoNotes.Infrastructure.Identity;

internal sealed class UserContext(
    IHttpContextAccessor httpContextAccessor,
    IAppUserRepository appUserRepository
) : IUserContext
{
    public async Task<Guid> GetAppUserIdAsync(CancellationToken cancellationToken)
    {
        var keycloakSub = httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("No authenticated user in the current request.");

        var appUser = await appUserRepository.FindByKeycloakSubAsync(keycloakSub, cancellationToken)
            ?? throw new InvalidOperationException($"No AppUser found for Keycloak sub '{keycloakSub}'.");

        return appUser.Id;
    }
}
