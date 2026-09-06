namespace CoNotes.Application.Abstractions;

public interface IUserContext
{
    Task<Guid> GetAppUserIdAsync(CancellationToken cancellationToken);
    bool IsAdmin();
}
