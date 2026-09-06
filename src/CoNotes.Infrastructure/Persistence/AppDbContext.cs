namespace CoNotes.Infrastructure.Persistence;

internal sealed class AppDbContext(
    DbDataSource dataSource,
    IAggregateRootChangeTracker tracker) : IDisposable, IAsyncDisposable
{
    private DbConnection? _connection;
    public DbTransaction? Transaction
    {
        get;
        set
        {
            if (value is null)
            {
                field = null;
                return;
            }

            if (field is not null)
                throw new InvalidOperationException("Transaction has been set!");

            field = value;
        }
    }

    public async Task<DbConnection> GetDbConnectionAsync(CancellationToken ct)
    {
        _connection ??= await dataSource.OpenConnectionAsync(ct);

        return _connection;
    }

    public void TrackAggregateRoot(IAggregateRoot aggregateRoot)
    {
        tracker.Enqueue(aggregateRoot);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
