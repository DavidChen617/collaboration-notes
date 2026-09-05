import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { NoteService } from '../note.service';

@Component({
  selector: 'app-note-editor',
  imports: [FormsModule],
  template: `
    <h1>{{ noteId() ? '編輯筆記' : '建立新筆記' }}</h1>

    <form (ngSubmit)="save()">
      <label>
        標題
        <input type="text" [(ngModel)]="title" name="title" required />
      </label>

      <label>
        內容
        <textarea [(ngModel)]="content" name="content"></textarea>
      </label>

      <button type="submit">儲存</button>
    </form>
  `,
  styles: [],
})
export class NoteEditorComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly noteService = inject(NoteService);

  protected readonly noteId = signal<string | null>(null);
  protected title = '';
  protected content = '';

  constructor() {
    const id = this.route.snapshot.paramMap.get('noteId');
    this.noteId.set(id);

    if (id) {
      this.noteService.get(id).subscribe((note) => {
        this.title = note.title;
        this.content = note.content;
      });
    }
  }

  protected save(): void {
    const id = this.noteId();

    const result$ = id
      ? this.noteService.update(id, this.title, this.content)
      : this.noteService.create(this.title, this.content);

    result$.subscribe(() => this.router.navigateByUrl('/notes'));
  }
}
