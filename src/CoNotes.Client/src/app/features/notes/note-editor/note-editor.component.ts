import { AfterViewInit, Component, ElementRef, OnDestroy, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import { firstValueFrom } from 'rxjs';

import { NoteService } from '../note.service';
import { NoteLinkNode } from '../tiptap/note-link-node';
import { NoteLinkSuggestion } from '../tiptap/note-link-suggestion';

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
        內容（輸入 <code>[</code> 可搜尋並連結其他筆記）
        <div #editorHost></div>
      </label>

      <button type="submit">儲存</button>
    </form>
  `,
  styles: [],
})
export class NoteEditorComponent implements AfterViewInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly noteService = inject(NoteService);

  private readonly editorHost = viewChild.required<ElementRef<HTMLDivElement>>('editorHost');
  private editor: Editor | null = null;
  private pendingContent = '';

  protected readonly noteId = signal<string | null>(null);
  protected title = '';

  constructor() {
    const id = this.route.snapshot.paramMap.get('noteId');
    this.noteId.set(id);

    if (id) {
      this.noteService.get(id).subscribe((note) => {
        this.title = note.title;
        this.pendingContent = note.content;
        this.editor?.commands.setContent(this.pendingContent);
        this.refreshLinkLabels();
      });
    }
  }

  ngAfterViewInit(): void {
    this.editor = new Editor({
      element: this.editorHost().nativeElement,
      extensions: [
        StarterKit,
        NoteLinkNode,
        NoteLinkSuggestion.configure({
          searchNotes: async (keyword) => {
            const response = await firstValueFrom(this.noteService.searchByTitle(keyword));
            return response.notes.map((note) => ({ noteId: note.noteId, title: note.title }));
          },
        }),
      ],
      content: this.pendingContent,
    });

    if (this.pendingContent) {
      this.refreshLinkLabels();
    }
  }

  ngOnDestroy(): void {
    this.editor?.destroy();
  }

  private refreshLinkLabels(): void {
    const editor = this.editor;
    if (!editor) return;

    this.noteService.getGraph().subscribe((graph) => {
      const titleByNoteId = new Map(graph.nodes.map((node) => [node.noteId, node.title]));
      const { tr } = editor.state;
      let changed = false;

      editor.state.doc.descendants((node, pos) => {
        if (node.type.name !== 'noteLink') return;
        const currentTitle = titleByNoteId.get(node.attrs['noteId']);
        if (currentTitle && currentTitle !== node.attrs['label']) {
          tr.setNodeAttribute(pos, 'label', currentTitle);
          changed = true;
        }
      });

      if (changed) {
        editor.view.dispatch(tr);
      }
    });
  }

  protected save(): void {
    const id = this.noteId();
    const content = this.editor?.getHTML() ?? '';

    const result$ = id
      ? this.noteService.update(id, this.title, content)
      : this.noteService.create(this.title, content);

    result$.subscribe(() => this.router.navigateByUrl('/notes'));
  }
}
