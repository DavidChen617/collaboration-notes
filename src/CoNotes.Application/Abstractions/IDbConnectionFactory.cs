using System.Data;

namespace CoNotes.Application.Abstractions;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
