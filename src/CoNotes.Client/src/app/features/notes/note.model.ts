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

/** Yjs update/snapshot 內容都是 base64 字串(對應後端 byte[] 的 JSON 序列化方式)。 */
export interface NoteHistory {
  baseSnapshot: string | null;
  subsequentUpdates: string[];
}

export interface ShareLinkResult {
  shareToken: string;
}
