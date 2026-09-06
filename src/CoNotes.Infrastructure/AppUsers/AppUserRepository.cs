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
                created_at as {nameof(AppUserRow.CreatedOnUtc)},
                plan_tier as {nameof(AppUserRow.PlanTier)}
            from app_users
            where keycloak_sub = @KeycloakSub;
            """,
            new { KeycloakSub = keycloakSub },
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<AppUserRow>(cmd);

        return row is null ? null : ToAggregate(row);
    }

    public async Task<AppUser?> FindByIdAsync(Guid appUserId, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"""
            select
                id as {nameof(AppUserRow.Id)},
                keycloak_sub as {nameof(AppUserRow.KeycloakSub)},
                created_at as {nameof(AppUserRow.CreatedOnUtc)},
                plan_tier as {nameof(AppUserRow.PlanTier)}
            from app_users
            where id = @AppUserId;
            """,
            new { AppUserId = appUserId },
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<AppUserRow>(cmd);

        return row is null ? null : ToAggregate(row);
    }

    public async Task<Result> AddAsync(AppUser appUser, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"""
            insert into app_users (id, keycloak_sub, created_at, plan_tier)
            values (@Id, @KeycloakSub, @CreatedOnUtc, @PlanTier);
            """,
            new
            {
                appUser.Id,
                appUser.KeycloakSub,
                appUser.CreatedOnUtc,
                PlanTier = appUser.PlanTier.ToString(),
            },
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        await connection.ExecuteAsync(cmd);

        appDbContext.TrackAggregateRoot(appUser);

        return Result.Success();
    }

    public async Task<Result> UpdateAsync(AppUser appUser, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"""
            update app_users
            set plan_tier = @PlanTier
            where id = @Id;
            """,
            new { appUser.Id, PlanTier = appUser.PlanTier.ToString() },
            cancellationToken: ct,
            transaction: appDbContext.Transaction);

        var connection = await appDbContext.GetDbConnectionAsync(ct);

        await connection.ExecuteAsync(cmd);

        appDbContext.TrackAggregateRoot(appUser);

        return Result.Success();
    }

    private static AppUser ToAggregate(AppUserRow row) =>
        AppUser.Rehydrate(row.Id, row.KeycloakSub, row.CreatedOnUtc, Enum.Parse<PlanTier>(row.PlanTier));

    private sealed record AppUserRow(Guid Id, string KeycloakSub, DateTime CreatedOnUtc, string PlanTier);
}
