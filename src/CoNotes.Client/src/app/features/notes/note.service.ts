import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../../core/app-config';
import { NoteDetail, NoteSummary } from './note.model';

interface ListNotesResponse {
  notes: NoteSummary[];
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
}
