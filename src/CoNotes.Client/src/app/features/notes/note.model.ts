export interface NoteSummary {
  noteId: string;
  title: string;
  content: string;
  createdOnUtc: string;
  updatedOnUtc: string;
}

export interface NoteDetail {
  noteId: string;
  title: string;
  content: string;
  createdOnUtc: string;
  updatedOnUtc: string;
}

export interface NoteSearchResult {
  noteId: string;
  title: string;
}

export interface NoteGraphNode {
  noteId: string;
  title: string;
}

export interface NoteGraphEdge {
  sourceNoteId: string;
  targetNoteId: string;
}

export interface NoteGraph {
  nodes: NoteGraphNode[];
  edges: NoteGraphEdge[];
}
