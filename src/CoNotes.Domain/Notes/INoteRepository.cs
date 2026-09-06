namespace CoNotes.Domain.Notes;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(Guid noteId, CancellationToken ct);
    Task<Result> AddAsync(Note note, CancellationToken ct);
    Task<Result> UpdateAsync(Note note, CancellationToken ct);
    Task<Result> DeleteAsync(Note note, CancellationToken ct);

    /// <summary>
    /// Of <paramref name="candidateNoteIds"/>, returns the subset that exist and are owned by
    /// <paramref name="ownerAppUserId"/> - used to validate wikilink targets before calling
    /// <see cref="Note.ResolveLinks"/>.
    /// </summary>
    Task<IReadOnlySet<Guid>> FindOwnedNoteIdsAsync(Guid ownerAppUserId, IReadOnlyCollection<Guid> candidateNoteIds, CancellationToken ct);
}
