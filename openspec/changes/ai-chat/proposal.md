**Bounded Context**: Notes｜**Aggregate**: `ChatMessage`

## Why

這是 ProMax 訂閱等級的差異化功能：筆記編輯畫面右下角有一個聊天室，讓筆記的擁有者與共編者可以彼此聊天，也可以用 `@AI` 標記觸發一個了解筆記內容的 AI 助理加入對話。訂閱等級的解鎖邏輯本身留給後面的 `subscription-billing` change 處理。

## What Changes

- 筆記編輯畫面新增一個聊天室，範圍是單一筆記；能存取的人跟這篇筆記的共編者名單一致（擁有者 + 共編者）。
- 使用者在訊息中標記 `@AI` 時，系統會非同步呼叫 AI provider 產生回覆，回覆以聊天訊息的形式呈現在同一個聊天室，所有參與者都看得到。
- 送給 AI 的 context 包含最近的聊天對話歷史，以及這篇筆記目前的內容。
- 設定多個免費額度的 AI provider，用 Chain of Responsibility 依序嘗試：目前使用的 provider 無法回應時（例如額度用盡），自動改用下一個，直到有一個成功回應或全部都失敗。
- 新增 Postgres schema：`ChatMessage`（記錄人類與 AI 的訊息，關聯到筆記）。

## Capabilities

### New Capabilities
- `ai-chat`：筆記的擁有者與共編者可以在該筆記的聊天室互相傳訊息，並可用 `@AI` 觸發一個看得到聊天歷史與筆記內容的 AI 助理回覆；當設定的 AI provider 無法回應時，系統自動改用下一個備援 provider。

### Modified Capabilities
（無 —— 存取範圍沿用 `collab-editing` 已建立的共編者名單概念，不修改任何既有 capability 的需求）

## Impact

- 新增 Postgres table：`ChatMessage`。
- 新增 SignalR Hub 用於聊天室的即時訊息傳遞，沿用既有的 Redis backplane。
- 新增後端的 AI provider 抽象層與 Chain of Responsibility 依序 failover 邏輯，需要設定多組免費額度 AI provider 的憑證。
- Angular 前端：筆記編輯畫面新增聊天室 UI（右下角）。
- **依賴前提**：聊天室的存取範圍（誰能看到/傳訊息）沿用 `collab-editing` 建立的共編者名單，因此實作順序上這個 change 應該排在 `collab-editing` 之後。
