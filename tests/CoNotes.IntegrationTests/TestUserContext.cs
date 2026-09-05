using CoNotes.Application.Abstractions;

namespace IntegrationTests;

public sealed class TestUserContext : IUserContext
{
    public Guid AppUserId { get; set; }

    public Task<Guid> GetAppUserIdAsync(CancellationToken cancellationToken) => Task.FromResult(AppUserId);
}
