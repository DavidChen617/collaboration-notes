namespace CoNotes.Domain.AppUsers;

public interface IAppUserRepository
{
    Task<AppUser?> FindByKeycloakSubAsync(string keycloakSub, CancellationToken ct);
    Task<Result> AddAsync(AppUser appUser, CancellationToken ct);
}
