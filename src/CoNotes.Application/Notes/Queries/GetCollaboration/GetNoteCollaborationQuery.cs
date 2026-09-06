namespace CoNotes.Application.Notes.Queries.GetCollaboration;

public sealed record GetNoteCollaborationQuery(Guid NoteId) : IQuery<Result<GetNoteCollaborationDto>>;

public sealed record GetNoteCollaborationDto(string? ShareToken, IReadOnlyCollection<Guid> CollaboratorAppUserIds);
