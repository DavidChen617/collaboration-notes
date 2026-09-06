import { AfterViewInit, Component, ElementRef, OnDestroy, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Editor, Extensions } from '@tiptap/core';
import Collaboration from '@tiptap/extension-collaboration';
import StarterKit from '@tiptap/starter-kit';
import { firstValueFrom } from 'rxjs';
import * as Y from 'yjs';

import { NoteCollabService, NoteCollabSession, fromBase64 } from '../note-collab.service';
import { NoteCollaboration } from '../note.model';
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

    @if (collaboration(); as settings) {
      <section>
        <h2>共編管理</h2>

        @if (settings.shareToken) {
          <label>
            分享連結
            <input type="text" readonly [value]="shareUrl()" />
          </label>
          <button type="button" (click)="revokeShareLink()">撤銷並換發</button>
        } @else {
          <button type="button" (click)="generateShareLink()">產生分享連結</button>
        }

        <h3>共編者</h3>
        @if (settings.collaboratorAppUserIds.length === 0) {
          <p>目前沒有共編者。</p>
        } @else {
          <ul>
            @for (collaboratorAppUserId of settings.collaboratorAppUserIds; track collaboratorAppUserId) {
              <li>
                <code>{{ collaboratorAppUserId }}</code>
                <button type="button" (click)="removeCollaborator(collaboratorAppUserId)">移除</button>
              </li>
            }
          </ul>
        }
      </section>
    }

    @if (noteId()) {
      <section>
        <h2>編輯歷史</h2>
        <label>
          回放時間
          <input type="datetime-local" step="0.001" [(ngModel)]="historyAt" name="historyAt" />
        </label>
        <button type="button" (click)="replayHistory()">回放</button>
        @if (replayedAt()) {
          <p>回放時間：<time>{{ replayedAt() }}</time></p>
        }
        <div #historyEditorHost [hidden]="!replayedAt()"></div>
      </section>
    }
  `,
  styles: [],
})
export class NoteEditorComponent implements AfterViewInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly noteService = inject(NoteService);
  private readonly noteCollabService = inject(NoteCollabService);

  private readonly editorHost = viewChild.required<ElementRef<HTMLDivElement>>('editorHost');
  private readonly historyEditorHost = viewChild<ElementRef<HTMLDivElement>>('historyEditorHost');
  private editor: Editor | null = null;
  private collabSession: NoteCollabSession | null = null;
  private historyEditor: Editor | null = null;
  private historyDoc: Y.Doc | null = null;
  private originalContent = '';

  protected readonly noteId = signal<string | null>(null);
  protected readonly collaboration = signal<NoteCollaboration | null>(null);
  protected readonly shareUrl = computed(() => {
    const shareToken = this.collaboration()?.shareToken;
    return shareToken ? `${globalThis.location.origin}/share/${shareToken}` : '';
  });
  protected readonly replayedAt = signal('');
  protected title = '';
  protected historyAt = toLocalDateTimeInputValue(new Date());

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
    this.historyEditor?.destroy();
    this.historyDoc?.destroy();
    void this.collabSession?.disconnect();
  }

  private async initializeCollaborativeEditor(noteId: string): Promise<void> {
    const note = await firstValueFrom(this.noteService.get(noteId));
    this.title = note.title;
    this.originalContent = note.content;
    this.loadCollaboration(noteId);

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

  private loadCollaboration(noteId: string): void {
    this.noteService.getCollaboration(noteId).subscribe({
      next: (settings) => this.collaboration.set(settings),
      error: () => this.collaboration.set(null),
    });
  }

  protected generateShareLink(): void {
    const noteId = this.noteId();
    if (!noteId) return;

    this.noteService.generateShareLink(noteId).subscribe(({ shareToken }) => {
      this.collaboration.update((settings) => settings ? { ...settings, shareToken } : settings);
    });
  }

  protected revokeShareLink(): void {
    const noteId = this.noteId();
    if (!noteId) return;

    this.noteService.revokeShareLink(noteId).subscribe(({ shareToken }) => {
      this.collaboration.update((settings) => settings ? { ...settings, shareToken } : settings);
    });
  }

  protected removeCollaborator(collaboratorAppUserId: string): void {
    const noteId = this.noteId();
    if (!noteId) return;

    this.noteService.removeCollaborator(noteId, collaboratorAppUserId).subscribe(() => {
      this.collaboration.update((settings) => settings ? {
        ...settings,
        collaboratorAppUserIds: settings.collaboratorAppUserIds.filter((id) => id !== collaboratorAppUserId),
      } : settings);
    });
  }

  protected async replayHistory(): Promise<void> {
    const noteId = this.noteId();
    const historyEditorHost = this.historyEditorHost();
    if (!noteId || !this.historyAt || !historyEditorHost) return;

    const atUtc = new Date(this.historyAt).toISOString();
    const history = await firstValueFrom(this.noteService.getHistory(noteId, atUtc));
    const doc = new Y.Doc();

    if (history.baseSnapshot) {
      Y.applyUpdate(doc, fromBase64(history.baseSnapshot), 'remote');
    }
    for (const update of history.subsequentUpdates) {
      Y.applyUpdate(doc, fromBase64(update), 'remote');
    }

    this.historyEditor?.destroy();
    this.historyDoc?.destroy();

    const fragment = doc.getXmlFragment('default');
    if (fragment.length === 0) {
      doc.destroy();
      this.historyDoc = null;
      this.historyEditor = new Editor({
        element: historyEditorHost.nativeElement,
        extensions: [StarterKit, NoteLinkNode],
        content: this.originalContent,
        editable: false,
      });
    } else {
      this.historyDoc = doc;
      this.historyEditor = new Editor({
        element: historyEditorHost.nativeElement,
        extensions: [
          StarterKit.configure({ undoRedo: false }),
          Collaboration.configure({ document: doc }),
          NoteLinkNode,
        ],
        editable: false,
      });
    }

    this.replayedAt.set(new Date(atUtc).toLocaleString());
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

function toLocalDateTimeInputValue(date: Date): string {
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return localDate.toISOString().slice(0, 23);
}
