import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { NoteService } from '../note.service';
import { NoteSummary } from '../note.model';

@Component({
  selector: 'app-note-list',
  imports: [RouterLink],
  template: `
    <h1>我的筆記</h1>

    <a routerLink="/notes/new">建立新筆記</a>
    <a routerLink="/notes/graph">檢視關係圖</a>

    @if (notes().length === 0) {
      <p>還沒有任何筆記。</p>
    } @else {
      <ul>
        @for (note of notes(); track note.noteId) {
          <li>
            <a [routerLink]="['/notes', note.noteId, 'edit']">{{ note.title }}</a>
            <button type="button" (click)="deleteNote(note)">刪除</button>
          </li>
        }
      </ul>
    }
  `,
  styles: [],
})
export class NoteListComponent {
  private readonly noteService = inject(NoteService);

  protected readonly notes = signal<NoteSummary[]>([]);

  constructor() {
    this.noteService.list().subscribe((response) => this.notes.set(response.notes));
  }

  protected deleteNote(note: NoteSummary): void {
    this.noteService.delete(note.noteId).subscribe(() => {
      this.notes.update((notes) => notes.filter((n) => n.noteId !== note.noteId));
    });
  }
}
