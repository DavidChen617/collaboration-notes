import { Routes } from '@angular/router';

import { NoteListComponent } from './features/notes/note-list/note-list.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'notes' },
  { path: 'notes', component: NoteListComponent },
  {
    path: 'notes/new',
    loadComponent: () =>
      import('./features/notes/note-editor/note-editor.component').then((m) => m.NoteEditorComponent),
  },
  {
    path: 'notes/graph',
    loadComponent: () =>
      import('./features/notes/note-graph/note-graph.component').then((m) => m.NoteGraphComponent),
  },
  {
    path: 'notes/:noteId/edit',
    loadComponent: () =>
      import('./features/notes/note-editor/note-editor.component').then((m) => m.NoteEditorComponent),
  },
];
