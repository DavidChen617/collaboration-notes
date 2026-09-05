using CoNotes.Domain.AppUsers;

namespace CoNotes.Infrastructure.AppUsers;

internal sealed class AppUserRepository(IDbConnectionFactory dbConnectionFactory) : IAppUserRepository
{
    public async Task<AppUser?> FindByKeycloakSubAsync(string keycloakSub, CancellationToken ct)
    {
        using var connection = await dbConnectionFactory.CreateConnectionAsync(ct);

        var cmd = new CommandDefinition(
            $"""
            select
                id as {nameof(AppUserRow.Id)},
                keycloak_sub as {nameof(AppUserRow.KeycloakSub)},
                created_at as {nameof(AppUserRow.CreatedAt)}
            from app_users
            where keycloak_sub = @KeycloakSub;
            """,
            new { KeycloakSub = keycloakSub },
            cancellationToken: ct);

        var row = await connection.QuerySingleOrDefaultAsync<AppUserRow>(cmd);

        return row is null
            ? null
            : AppUser.Rehydrate(row.Id, row.KeycloakSub, row.CreatedAt);
    }

    public async Task<Result> AddAsync(AppUser appUser, CancellationToken ct)
    {
        using var connection = await dbConnectionFactory.CreateConnectionAsync(ct);

        var cmd = new CommandDefinition(
            $"""
            insert into app_users (id, keycloak_sub, created_at)
            values (@{nameof(appUser.Id)}, @{nameof(appUser.KeycloakSub)}, @{nameof(appUser.CreatedAt)});
            """,
            appUser,
            cancellationToken: ct);

        await connection.ExecuteAsync(cmd);

        return Result.Success();
    }

    private sealed record AppUserRow(Guid Id, string KeycloakSub, DateTime CreatedAt);
}
