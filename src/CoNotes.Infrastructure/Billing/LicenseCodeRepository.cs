using CoNotes.Domain.Billing;

namespace CoNotes.Infrastructure.Billing;

internal sealed class LicenseCodeRepository(AppDbContext appDbContext) : ILicenseCodeRepository
{
    public async Task AddAsync(LicenseCode licenseCode, CancellationToken ct)
    {
        var param = new
        {
            licenseCode.Id,
            licenseCode.Code,
            PlanTier = licenseCode.PlanTier.ToString(),
            licenseCode.PayPalOrderId,
            licenseCode.RedeemedByAppUserId,
            licenseCode.CreatedAt,
            licenseCode.RedeemedAt,
        };
        var command = new CommandDefinition(
            $"""
            insert into license_codes (
                id,
                code,
                plan_tier,
                paypal_order_id,
                redeemed_by_app_user_id,
                created_at,
                redeemed_at
            )
            values (
                @{nameof(param.Id)},
                @{nameof(param.Code)},
                @{nameof(param.PlanTier)},
                @{nameof(param.PayPalOrderId)},
                @{nameof(param.RedeemedByAppUserId)},
                @{nameof(param.CreatedAt)},
                @{nameof(param.RedeemedAt)}
            );
            """,
            param,
            cancellationToken: ct,
            transaction: appDbContext.Transaction
        );
        var connection = await appDbContext.GetDbConnectionAsync(ct);

        await connection.ExecuteAsync(command);
        appDbContext.TrackAggregateRoot(licenseCode);
    }

    public async Task<LicenseCode?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var command = new CommandDefinition(
            $"""
            {SelectColumns}
            from license_codes
            where code = @Code;
            """,
            new { Code = code },
            cancellationToken: ct,
            transaction: appDbContext.Transaction
        );
        var connection = await appDbContext.GetDbConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<LicenseCodeRow>(command);

        return row is null ? null : ToAggregate(row);
    }

    public async Task<LicenseCode?> GetByPayPalOrderIdAsync(string payPalOrderId, CancellationToken ct)
    {
        var command = new CommandDefinition(
            $"""
            {SelectColumns}
            from license_codes
            where paypal_order_id = @PayPalOrderId;
            """,
            new { PayPalOrderId = payPalOrderId },
            cancellationToken: ct,
            transaction: appDbContext.Transaction
        );
        var connection = await appDbContext.GetDbConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<LicenseCodeRow>(command);

        return row is null ? null : ToAggregate(row);
    }

    private const string SelectColumns = $"""
        select
            id as {nameof(LicenseCodeRow.Id)},
            code as {nameof(LicenseCodeRow.Code)},
            plan_tier as {nameof(LicenseCodeRow.PlanTier)},
            paypal_order_id as {nameof(LicenseCodeRow.PayPalOrderId)},
            redeemed_by_app_user_id as {nameof(LicenseCodeRow.RedeemedByAppUserId)},
            created_at as {nameof(LicenseCodeRow.CreatedAt)},
            redeemed_at as {nameof(LicenseCodeRow.RedeemedAt)}
        """;

    private static LicenseCode ToAggregate(LicenseCodeRow row) =>
        LicenseCode.Rehydrate(
            row.Id,
            row.Code,
            Enum.Parse<PlanTier>(row.PlanTier),
            row.PayPalOrderId,
            row.RedeemedByAppUserId,
            row.CreatedAt,
            row.RedeemedAt
        );

    public async Task<bool> TryPersistRedemptionAsync(LicenseCode licenseCode, CancellationToken ct)
    {
        var param = new
        {
            licenseCode.Code,
            licenseCode.RedeemedByAppUserId,
            licenseCode.RedeemedAt,
        };
        var command = new CommandDefinition(
            $"""
            update license_codes
            set redeemed_by_app_user_id = @{nameof(param.RedeemedByAppUserId)},
                redeemed_at = @{nameof(param.RedeemedAt)}
            where code = @{nameof(param.Code)}
                and redeemed_by_app_user_id is null;
            """,
            param,
            cancellationToken: ct,
            transaction: appDbContext.Transaction
        );
        var connection = await appDbContext.GetDbConnectionAsync(ct);
        var affectedRows = await connection.ExecuteAsync(command);

        if (affectedRows != 1)
            return false;

        appDbContext.TrackAggregateRoot(licenseCode);

        return true;
    }

    private sealed record LicenseCodeRow(
        Guid Id,
        string Code,
        string PlanTier,
        string PayPalOrderId,
        Guid? RedeemedByAppUserId,
        DateTime CreatedAt,
        DateTime? RedeemedAt
    );
}
