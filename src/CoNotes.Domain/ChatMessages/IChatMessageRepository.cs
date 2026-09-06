namespace CoNotes.Domain.ChatMessages;

public interface IChatMessageRepository
{
    Task<Result> AddAsync(ChatMessage chatMessage, CancellationToken ct);
    Task<IReadOnlyList<ChatMessage>> GetRecentByNoteIdAsync(Guid noteId, int limit, CancellationToken ct);
}
