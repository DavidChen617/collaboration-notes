namespace CoNotes.Application.Notes.Queries.GetGraph;

public sealed record GetNoteGraphQuery : IQuery<Result<NoteGraphDto>>;

public sealed record NoteGraphDto(List<NoteGraphNode> Nodes, List<NoteGraphEdge> Edges);

public sealed record NoteGraphNode(Guid NoteId, string Title);

public sealed record NoteGraphEdge(Guid SourceNoteId, Guid TargetNoteId);
