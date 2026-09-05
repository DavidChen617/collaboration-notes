import { Routes } from '@angular/router';

import { NoteEditorComponent } from './features/notes/note-editor/note-editor.component';
import { NoteListComponent } from './features/notes/note-list/note-list.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'notes' },
  { path: 'notes', component: NoteListComponent },
  { path: 'notes/new', component: NoteEditorComponent },
  { path: 'notes/:noteId/edit', component: NoteEditorComponent },
];
