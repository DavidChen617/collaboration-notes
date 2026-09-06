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
  {
    path: 'share/:shareToken',
    loadComponent: () =>
      import('./features/notes/note-share-join/note-share-join.component').then((m) => m.NoteShareJoinComponent),
  },
  {
    path: 'billing/plans',
    loadComponent: () =>
      import('./features/billing/billing-plans/billing-plans.component').then((m) => m.BillingPlansComponent),
  },
  {
    path: 'billing/confirm',
    loadComponent: () =>
      import('./features/billing/billing-confirm/billing-confirm.component').then((m) => m.BillingConfirmComponent),
  },
  {
    path: 'billing/redeem',
    loadComponent: () =>
      import('./features/billing/redeem-code/redeem-code.component').then((m) => m.RedeemCodeComponent),
  },
];
