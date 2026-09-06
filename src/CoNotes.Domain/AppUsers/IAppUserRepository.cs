namespace CoNotes.Domain.AppUsers;

public interface IAppUserRepository
{
    Task<AppUser?> FindByKeycloakSubAsync(string keycloakSub, CancellationToken ct);
    Task<AppUser?> FindByIdAsync(Guid appUserId, CancellationToken ct);
    Task<Result> AddAsync(AppUser appUser, CancellationToken ct);
    Task<Result> UpdateAsync(AppUser appUser, CancellationToken ct);
}
