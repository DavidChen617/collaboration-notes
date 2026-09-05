using CoNotes.Domain.AppUsers;
using CoNotes.Infrastructure.Persistence;

namespace CoNotes.Infrastructure.AppUsers;

internal sealed class AppUserRepository(AppDbContext appDbContext) : IAppUserRepository
{
    public async Task<AppUser?> FindByKeycloakSubAsync(string keycloakSub, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"""
            select
                id as {nameof(AppUserRow.Id)},
                keycloak_sub as {nameof(AppUserRow.KeycloakSub)},
                created_at as {nameof(AppUserRow.CreatedOnUtc)}
            from app_users
            where keycloak_sub = @KeycloakSub;
            """,
            new { KeycloakSub = keycloakSub },
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<AppUserRow>(cmd);

        return row is null
            ? null
            : AppUser.Rehydrate(row.Id, row.KeycloakSub, row.CreatedOnUtc);
    }

    public async Task<Result> AddAsync(AppUser appUser, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"""
            insert into app_users (id, keycloak_sub, created_at)
            values (@{nameof(appUser.Id)}, @{nameof(appUser.KeycloakSub)}, @{nameof(appUser.CreatedOnUtc)});
            """,
            appUser,
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        await connection.ExecuteAsync(cmd);

        appDbContext.TrackAggregateRoot(appUser);

        return Result.Success();
    }

    private sealed record AppUserRow(Guid Id, string KeycloakSub, DateTime CreatedOnUtc);
}
