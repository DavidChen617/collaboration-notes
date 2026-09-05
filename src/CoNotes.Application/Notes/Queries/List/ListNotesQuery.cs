namespace CoNotes.Application.Notes.Queries.List;

public sealed record ListNotesQuery : IQuery<Result<ListNotesDto>>;

public sealed record ListNotesDto(List<NoteItem> Notes);

public sealed record NoteItem(Guid NoteId, string Title, string Content, DateTime CreatedOnUtc, DateTime UpdatedOnUtc);
