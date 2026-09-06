import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../../core/app-config';
import {
  NoteCollaboration,
  NoteDetail,
  NoteGraph,
  NoteHistory,
  NoteSearchResult,
  NoteSummary,
  ShareLinkResult,
} from './note.model';

interface ListNotesResponse {
  notes: NoteSummary[];
}

interface SearchNotesResponse {
  notes: NoteSearchResult[];
}

interface CreateNoteResponse {
  noteId: string;
}

interface UpdateNoteResponse {
  noteId: string;
  title: string;
  content: string;
}

@Injectable({ providedIn: 'root' })
export class NoteService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/api/v1/notes`;

  list(): Observable<ListNotesResponse> {
    return this.http.get<ListNotesResponse>(this.baseUrl);
  }

  get(noteId: string): Observable<NoteDetail> {
    return this.http.get<NoteDetail>(`${this.baseUrl}/${noteId}`);
  }

  create(title: string, content: string): Observable<CreateNoteResponse> {
    return this.http.post<CreateNoteResponse>(this.baseUrl, { title, content });
  }

  update(noteId: string, title: string, content: string): Observable<UpdateNoteResponse> {
    return this.http.put<UpdateNoteResponse>(`${this.baseUrl}/${noteId}`, { title, content });
  }

  delete(noteId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${noteId}`);
  }

  searchByTitle(keyword: string): Observable<SearchNotesResponse> {
    return this.http.get<SearchNotesResponse>(`${this.baseUrl}/search`, { params: { keyword } });
  }

  getGraph(): Observable<NoteGraph> {
    return this.http.get<NoteGraph>(`${this.baseUrl}/graph`);
  }

  getHistory(noteId: string, atUtc?: string): Observable<NoteHistory> {
    const params = atUtc ? { at: atUtc } : undefined;
    return this.http.get<NoteHistory>(`${this.baseUrl}/${noteId}/history`, { params });
  }

  getCollaboration(noteId: string): Observable<NoteCollaboration> {
    return this.http.get<NoteCollaboration>(`${this.baseUrl}/${noteId}/collaboration`);
  }

  generateShareLink(noteId: string): Observable<ShareLinkResult> {
    return this.http.post<ShareLinkResult>(`${this.baseUrl}/${noteId}/share-link`, null);
  }

  revokeShareLink(noteId: string): Observable<ShareLinkResult> {
    return this.http.post<ShareLinkResult>(`${this.baseUrl}/${noteId}/share-link/revoke`, null);
  }

  joinViaShareLink(shareToken: string): Observable<{ noteId: string }> {
    return this.http.post<{ noteId: string }>(`${this.baseUrl}/share-link/${shareToken}/join`, null);
  }

  removeCollaborator(noteId: string, collaboratorAppUserId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${noteId}/collaborators/${collaboratorAppUserId}`);
  }
}
