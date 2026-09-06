using Davish.Sendr;

namespace CoNotes.Infrastructure.Persistence;

internal sealed class UnitOfWork(
    AppDbContext appDbContext,
    IAggregateRootChangeTracker tracker,
    IPublisher publisher
) : IUnitOfWork
{
    public async Task BeginAsync(CancellationToken ct)
    {
        var conn = await appDbContext.GetDbConnectionAsync(ct);

        appDbContext.Transaction = await conn.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        if (appDbContext.Transaction is null)
            return;

        var transaction = appDbContext.Transaction;

        try
        {
            await transaction.CommitAsync(ct);
        }
        finally
        {
            appDbContext.Transaction = null;
            await transaction.DisposeAsync();
        }

        // 發布事件必須在上面的 transaction 已經 commit、且 appDbContext.Transaction 已經清空之後才做:
        // 事件處理者(例如 LicenseCodeRedeemedDomainEventHandler)可能會再透過 Repository 寫入 DB,
        // 若還帶著這個(已經 commit 過的)Transaction 物件下去, Npgsql 會直接丟例外。這裡故意讓
        // 事件處理者的寫入落在原本這個交易之外、各自獨立 autocommit, 而不是原交易的一部分。
        await PublishDomainEvens(ct);
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (appDbContext.Transaction is null)
            return;

        var transaction = appDbContext.Transaction;

        try
        {
            await transaction.RollbackAsync(ct);
        }
        finally
        {
            appDbContext.Transaction = null;
            await transaction.DisposeAsync();
        }
    }

    private async Task PublishDomainEvens(CancellationToken ct)
    {
        var domainEvents = new List<IDomainEvent>();

        foreach (var aggregate in tracker.Dequeue())
        {
            domainEvents.AddRange(aggregate.DomainEvents);
            aggregate.ClearDomainEvents();
        }

        if (domainEvents.Count > 0)
            foreach (var domainEvent in domainEvents)
                await publisher.PublishAsync(domainEvent, ct);
    }
}
