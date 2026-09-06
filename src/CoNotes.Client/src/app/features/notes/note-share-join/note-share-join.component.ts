import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { NoteService } from '../note.service';

@Component({
  selector: 'app-note-share-join',
  template: `
    @if (errorMessage()) {
      <p role="alert">{{ errorMessage() }}</p>
    } @else {
      <p>正在加入共編...</p>
    }
  `,
})
export class NoteShareJoinComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly noteService = inject(NoteService);

  protected readonly errorMessage = signal('');

  constructor() {
    const shareToken = this.route.snapshot.paramMap.get('shareToken');
    if (!shareToken) {
      this.errorMessage.set('分享連結無效或已失效。');
      return;
    }

    this.noteService.joinViaShareLink(shareToken).subscribe({
      next: ({ noteId }) => this.router.navigate(['/notes', noteId, 'edit']),
      error: () => this.errorMessage.set('分享連結無效或已失效。'),
    });
  }
}
