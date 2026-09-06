import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HttpTransportType } from '@microsoft/signalr';
import Keycloak from 'keycloak-js';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../../core/app-config';
import { ChatMessage } from './note.model';

export interface ChatSession {
  connection: HubConnection;
  send: (content: string) => Promise<void>;
  disconnect: () => Promise<void>;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);
  private readonly keycloak = inject(Keycloak);
  private readonly baseUrl = `${API_BASE_URL}/api/v1/notes`;

  getHistory(noteId: string): Observable<{ messages: ChatMessage[] }> {
    return this.http.get<{ messages: ChatMessage[] }>(
      `${this.baseUrl}/${noteId}/chat/messages`
    );
  }

  async connect(
    noteId: string,
    onMessage: (message: ChatMessage) => void
  ): Promise<ChatSession> {
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/chat`, {
        accessTokenFactory: () => this.keycloak.token ?? '',
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .build();

    connection.on('ReceiveMessage', onMessage);
    const join = () => connection.invoke('JoinNoteAsync', noteId);
    connection.onreconnected(() => void join());

    await connection.start();
    await join();

    return {
      connection,
      send: (content) => connection.invoke('SendMessageAsync', noteId, content),
      disconnect: () => connection.stop(),
    };
  }
}
