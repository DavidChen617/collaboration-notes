namespace CoNotes.Domain.Notes;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(Guid noteId, CancellationToken ct);
    Task<Result> AddAsync(Note note, CancellationToken ct);
    Task<Result> UpdateAsync(Note note, CancellationToken ct);
    Task<Result> DeleteAsync(Note note, CancellationToken ct);
}
