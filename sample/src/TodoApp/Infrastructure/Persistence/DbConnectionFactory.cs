namespace Todo.Infrastructure.Persistence;

internal sealed class DbConnectionFactory(DbDataSource dataSource) : IDbConnectionFactory
{
    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await dataSource.OpenConnectionAsync(cancellationToken);
    }
}

