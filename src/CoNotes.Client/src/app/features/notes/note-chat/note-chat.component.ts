import { Component, ElementRef, Input, OnChanges, OnDestroy, SimpleChanges, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ChatService, ChatSession } from '../chat.service';
import { ChatMessage } from '../note.model';

@Component({
  selector: 'app-note-chat',
  imports: [FormsModule],
  template: `
    <section class="chat-panel">
      <h2>聊天室（可輸入 <code>@AI</code> 呼叫 AI 助理）</h2>

      <ul #messageList class="chat-messages">
        @for (message of messages(); track message.chatMessageId) {
          <li [class.ai-reply]="message.isAiReply">
            <span class="chat-author">{{ message.isAiReply ? 'AI' : (message.authorAppUserId ?? '未知使用者') }}</span>
            <span class="chat-content">{{ message.content }}</span>
          </li>
        }
      </ul>

      <form (ngSubmit)="send()">
        <input
          type="text"
          [(ngModel)]="draft"
          name="chatDraft"
          placeholder="輸入訊息，@AI 可呼叫 AI 助理"
          required
        />
        <button type="submit">送出</button>
      </form>
      @if (errorMessage()) {
        <p role="alert">{{ errorMessage() }}</p>
      }
    </section>
  `,
  styles: [
    `
      .chat-panel {
        position: fixed;
        right: 1rem;
        bottom: 1rem;
        width: 320px;
        max-height: 420px;
        display: flex;
        flex-direction: column;
        border: 1px solid #ccc;
        background: white;
        padding: 0.5rem;
      }

      .chat-messages {
        flex: 1;
        overflow-y: auto;
        list-style: none;
        margin: 0;
        padding: 0;
      }

      .chat-messages li {
        margin-bottom: 0.25rem;
      }

      .chat-messages li.ai-reply {
        font-style: italic;
      }

      .chat-author {
        font-weight: bold;
        margin-right: 0.25rem;
      }
    `,
  ],
})
export class NoteChatComponent implements OnChanges, OnDestroy {
  private readonly chatService = inject(ChatService);

  private readonly messageList = viewChild<ElementRef<HTMLUListElement>>('messageList');
  private session: ChatSession | null = null;

  @Input({ required: true }) noteId!: string;

  protected readonly messages = signal<ChatMessage[]>([]);
  protected readonly errorMessage = signal('');
  protected draft = '';

  async ngOnChanges(changes: SimpleChanges): Promise<void> {
    if (!changes['noteId'] || !this.noteId) return;

    await this.session?.disconnect();
    this.messages.set([]);

    const history = await firstValueFrom(this.chatService.getHistory(this.noteId));
    this.messages.set(history.messages);

    this.session = await this.chatService.connect(this.noteId, (message) => {
      this.messages.update((current) => [...current, message]);
      this.scrollToBottom();
    });

    this.scrollToBottom();
  }

  ngOnDestroy(): void {
    void this.session?.disconnect();
  }

  protected async send(): Promise<void> {
    const content = this.draft.trim();
    if (!content || !this.session) return;

    this.draft = '';
    this.errorMessage.set('');
    try {
      await this.session.send(content);
    } catch {
      this.errorMessage.set('傳送失敗：筆記擁有者的訂閱等級須為 ProMax 才能使用聊天室。');
    }
  }

  private scrollToBottom(): void {
    const element = this.messageList()?.nativeElement;
    if (!element) return;
    queueMicrotask(() => {
      element.scrollTop = element.scrollHeight;
    });
  }
}
