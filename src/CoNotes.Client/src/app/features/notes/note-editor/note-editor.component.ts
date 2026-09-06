import { AfterViewInit, Component, ElementRef, OnDestroy, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Editor, Extensions } from '@tiptap/core';
import Collaboration from '@tiptap/extension-collaboration';
import StarterKit from '@tiptap/starter-kit';
import { firstValueFrom } from 'rxjs';
import * as Y from 'yjs';

import { NoteCollabService, NoteCollabSession, fromBase64 } from '../note-collab.service';
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
  private readonly noteCollabService = inject(NoteCollabService);

  private readonly editorHost = viewChild.required<ElementRef<HTMLDivElement>>('editorHost');
  private editor: Editor | null = null;
  private collabSession: NoteCollabSession | null = null;

  protected readonly noteId = signal<string | null>(null);
  protected title = '';

  constructor() {
    this.noteId.set(this.route.snapshot.paramMap.get('noteId'));
  }

  async ngAfterViewInit(): Promise<void> {
    const id = this.noteId();

    if (id) {
      await this.initializeCollaborativeEditor(id);
    } else {
      this.editor = this.createEditor([]);
    }
  }

  ngOnDestroy(): void {
    this.editor?.destroy();
    void this.collabSession?.disconnect();
  }

  private async initializeCollaborativeEditor(noteId: string): Promise<void> {
    const note = await firstValueFrom(this.noteService.get(noteId));
    this.title = note.title;

    this.collabSession = await this.noteCollabService.connect(noteId);
    const history = await firstValueFrom(this.noteService.getHistory(noteId));

    // 兩者都用 'remote' 當 origin - 不只是排除真正跨用戶端的即時更新, 也排除這裡自己
    // 回放歷史紀錄造成的更新, 不然 NoteCollabService 裡的 doc.on('update', ...) 會把
    // 「剛從 server 讀回來的資料」當成本地新編輯, 又送一次回 server、寫進更新紀錄。
    if (history.baseSnapshot) {
      Y.applyUpdate(this.collabSession.doc, fromBase64(history.baseSnapshot), 'remote');
    }
    for (const update of history.subsequentUpdates) {
      Y.applyUpdate(this.collabSession.doc, fromBase64(update), 'remote');
    }

    const fragment = this.collabSession.doc.getXmlFragment('default');

    this.editor = this.createEditor([
      StarterKit.configure({ undoRedo: false }),
      Collaboration.configure({ document: this.collabSession.doc }),
    ]);

    if (fragment.length === 0) {
      // 這篇筆記從沒真的被即時協作編輯過(Y.Doc 是空的) - 用既有的 plain content 當初始內容,
      // Collaboration extension 會把這次 setContent 轉成第一筆 Yjs update 同步出去。
      this.editor.commands.setContent(note.content);
    }

    this.refreshLinkLabels();
  }

  private createEditor(extraExtensions: Extensions): Editor {
    return new Editor({
      element: this.editorHost().nativeElement,
      extensions: [
        ...(extraExtensions.length > 0 ? extraExtensions : [StarterKit]),
        NoteLinkNode,
        NoteLinkSuggestion.configure({
          searchNotes: async (keyword) => {
            const response = await firstValueFrom(this.noteService.searchByTitle(keyword));
            return response.notes.map((note) => ({ noteId: note.noteId, title: note.title }));
          },
        }),
      ],
    });
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
