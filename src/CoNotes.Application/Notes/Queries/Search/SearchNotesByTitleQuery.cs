namespace CoNotes.Application.Notes.Queries.Search;

public sealed record SearchNotesByTitleQuery(string Keyword) : IQuery<Result<SearchNotesByTitleDto>>;

public sealed record SearchNotesByTitleDto(List<NoteSearchResult> Notes);

public sealed record NoteSearchResult(Guid NoteId, string Title);
