namespace CoNotes.Application.Notes.Queries.Get;

public sealed record GetNoteQuery(Guid NoteId) : IQuery<Result<GetNoteDto>>;

public sealed record GetNoteDto(Guid NoteId, string Title, string Content, DateTime CreatedAt, DateTime UpdatedAt);
