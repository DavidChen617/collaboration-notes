namespace Todo.Infrastructure.Persistence;

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

            await PublishDomainEvens(ct);
        }
        finally
        {
            appDbContext.Transaction = null;
            await transaction.DisposeAsync();
        }
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

