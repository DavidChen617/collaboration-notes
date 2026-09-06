import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HttpTransportType } from '@microsoft/signalr';
import Keycloak from 'keycloak-js';
import * as Y from 'yjs';

import { API_BASE_URL } from '../../core/app-config';

/**
 * 一個筆記的即時協作連線:自己的 Y.Doc(用 fragment name `'default'`, 跟
 * NoteLinkEditor 裡 Collaboration extension 的預設值一致)+ 一條 SignalR HubConnection。
 * 後端 byte[] 在 JSON 上是 base64 字串(System.Text.Json 的預設行為), 所以這裡收發都用
 * base64 <-> Uint8Array 轉換, 不是直接傳位元組陣列。
 */
export interface NoteCollabSession {
  doc: Y.Doc;
  connection: HubConnection;
  disconnect: () => Promise<void>;
}

@Injectable({ providedIn: 'root' })
export class NoteCollabService {
  private readonly keycloak = inject(Keycloak);

  async connect(noteId: string): Promise<NoteCollabSession> {
    const doc = new Y.Doc();

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/notes`, {
        accessTokenFactory: () => this.keycloak.token ?? '',
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .build();

    connection.on('ReceiveUpdate', (updateBase64: string) => {
      Y.applyUpdate(doc, fromBase64(updateBase64), 'remote');
    });

    connection.on('SnapshotRequested', async () => {
      const snapshot = Y.encodeStateAsUpdate(doc);
      await connection.invoke('SaveSnapshotAsync', noteId, toBase64(snapshot));
    });

    connection.onreconnected(() => {
      void connection.invoke('JoinNoteAsync', noteId);
    });

    doc.on('update', (update: Uint8Array, origin: unknown) => {
      if (origin === 'remote') return;
      connection.invoke('SendUpdateAsync', noteId, toBase64(update)).catch(() => {});
    });

    await connection.start();
    await connection.invoke('JoinNoteAsync', noteId);

    return {
      doc,
      connection,
      disconnect: async () => {
        await connection.stop();
        doc.destroy();
      },
    };
  }
}

export function toBase64(bytes: Uint8Array): string {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
}

export function fromBase64(base64: string): Uint8Array {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}
