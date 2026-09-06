## 1. Domain

- [x] 1.1 在 `CoNotes.Domain` 定義 `ChatMessage` Aggregate Root（`Id`、`NoteId`、`AuthorAppUserId` 可為 null、`IsAiReply`、`Content`、`CreatedAt`），封裝「訊息內容含獨立 `@AI` 字詞」的判斷邏輯，建立時視情況發出 `ChatMessageSent`／`AiReplyRequested` Domain Event
- [x] 1.2 單元測試（`GivenXXX_WhenXXX_ThenXXX`）：`GivenMessageWithStandaloneAiMention_WhenChatMessageCreated_ThenRaisesAiReplyRequested`、`GivenMessageWithAiSubstringInsideEmail_WhenChatMessageCreated_ThenDoesNotRaiseAiReplyRequested`、`GivenPlainHumanMessage_WhenChatMessageCreated_ThenRaisesChatMessageSentOnly`
- [x] 1.3 定義 `IChatMessageRepository`（`AddAsync`、`GetRecentByNoteIdAsync`）與 `IAiChatProvider`（`TryGetReplyAsync(context) -> Reply?`）介面，供 Application 層依賴、Infrastructure 層實作

## 2. Application（Command / Query）

- [x] 2.1 實作 `SendChatMessageCommand` + Handler：驗證呼叫者為擁有者或共編者、儲存訊息、透過 ChatHub 廣播；單元測試（mock `IChatMessageRepository`）涵蓋擁有者/共編者傳送成功、非擁有者非共編者被拒絕（Application 透過 `IChatMessageBroadcaster` 抽象廣播，SignalR adapter 於 3.4 實作）
- [x] 2.2 實作 `GenerateAiReplyCommand` + Handler（由 `AiReplyRequested` 觸發的背景流程）：組合「筆記目前內容（截斷過長內容）」與「最近對話歷史」當 context，依序呼叫 `IAiChatProvider` chain（`AiReplyRequestedDomainEventHandler` 透過 `IAiReplyRequestQueue` 排入背景流程，不阻塞原始傳訊 request）
- [x] 2.3 單元測試（mock 多個 `IAiChatProvider`，NSubstitute）：`GivenPrimaryProviderUnavailable_WhenGeneratingAiReply_ThenFallbackProviderIsUsed`、`GivenAllProvidersUnavailable_WhenGeneratingAiReply_ThenAiReplyFailedRaisedAndFallbackMessageStored`、`GivenAnyProviderSucceeds_WhenGeneratingAiReply_ThenAiReplyGeneratedRaisedWithReplyContent`
- [x] 2.4 單元測試：`GivenNoteContentExceedsLengthLimit_WhenBuildingAiContext_ThenContentIsTruncated`
- [x] 2.5 實作 `GetChatHistoryQuery` + Handler（直接 Dapper 查詢，不經 Domain 層），單元測試涵蓋只有擁有者或共編者能取得歷史訊息（依 repo 既有純 Dapper Query 慣例，改由 Testcontainers + 真實 PostgreSQL 整合測試驗證擁有者與共編者可讀、無關使用者被拒絕）

## 3. Infrastructure

- [x] 3.1 撰寫 migration 新增 `ChatMessage` table（`NoteId` 外鍵、`AuthorAppUserId` 可為 null、`IsAiReply` 布林、`Content`、`CreatedAt`），執行後用 `migrate version` 確認套用成功；驗證 down 可正確移除（本機 PostgreSQL 實測版本由 `20260906130000` down 回 `20260906120100`，再 up 回 `20260906130000`）
- [x] 3.2 實作 `ChatMessage` 的 Dapper Repository，Testcontainers 整合測試：`GivenChatMessageSaved_WhenQueriedByNoteId_ThenReturnsMessageInOrder`
- [x] 3.3 定義至少兩個 `IAiChatProvider` 的免費額度 provider adapter 實作（Groq OpenAI-compatible Chat Completions 與 Google Gemini `generateContent` REST adapter；API key 由 configuration 讀取，未設定或 provider 失敗時回傳 null 交由 chain fallback）
- [x] 3.4 建立獨立的 SignalR ChatHub，以 `NoteId` 當 Group，加入前執行跟 Notes API 一致的存取檢查（擁有者或共編者），沿用既有 Redis backplane（`NoteCollabHubReplicaTests` 新增無關使用者加入 ChatHub 被拒絕的測試；兩個 API replica/Redis SignalR 測試 2/2 通過）

## 4. Api

- [x] 4.1 新增傳送訊息、取得聊天歷史的 Minimal API Endpoint / Hub method，只做輸入驗證並分派給對應 Command/Query Handler（REST `POST/GET /api/v1/notes/{noteId}/chat/messages`，Hub `JoinNoteAsync`/`SendMessageAsync`）
- [x] 4.2 Functional Test：`GivenOwnerOrCollaborator_WhenSendingChatMessage_ThenMessageStoredAndBroadcast`（owner 與 collaborator 傳送後可從 history 讀回，HTTP functional tests 12/12 通過）
- [x] 4.3 Functional Test：`GivenNeitherOwnerNorCollaborator_WhenAccessingChatRoom_ThenRequestRejected`（unrelated user 的 history/send 皆回 400）
- [x] 4.4 Functional Test：`GivenMessageWithAiMention_WhenProcessed_ThenAiReplyEventuallyAppearsInChatRoom`（@AI 觸發背景流程，在所有 provider 未設定時仍非同步留下「AI 目前無法回應」訊息）
- [x] 4.5 架構測試：驗證 `CoNotes.Domain` 不參考 `CoNotes.Infrastructure`/`CoNotes.Api`（架構測試 1/1 通過）

## 5. 前端聊天室 UI

> 新增 `NoteChatComponent`（`features/notes/note-chat/note-chat.component.ts`），固定定位在畫面右下角；掛載時（`ngOnChanges` 偵測 `noteId` 變化)先呼叫既有的 `ChatService.getHistory(noteId)` 灌入歷史訊息，再呼叫 `ChatService.connect(noteId, onMessage)` 加入 ChatHub 的 SignalR 群組並訂閱 `ReceiveMessage`；因為 `ChatMessageBroadcaster` 是對整個 Group 廣播（不是 `OthersInGroup`），自己送出的訊息也會透過同一個 `ReceiveMessage` 事件收到，所以畫面上不用另外手動 append 一次，避免重複。掛在 `NoteEditorComponent`：`@if (noteId(); as id) { <app-note-chat [noteId]="id" /> }`，只有既有筆記（有 `noteId`）才顯示，新筆記建立前不顯示。

- [x] 5.1 在筆記編輯畫面右下角新增聊天室面板，串接 ChatHub 與歷史訊息 API（`tsc --noEmit` 與 `ng build` 皆通過，`note-editor-component` lazy chunk 613KB→614KB，尺寸增加符合預期）
- [x] 5.2 驗證多個使用者同時在同一篇筆記的聊天室裡，彼此的訊息即時同步（用兩個臨時 Keycloak 使用者、兩個獨立 Playwright Chromium context 實測完整真實流程：登入 → 建立筆記 → 產生分享連結 → 第二個使用者透過連結加入 → 雙方都看得到聊天室面板 → A 傳送含當次唯一字串的訊息、B 在 10 秒內即時收到；反向 B 傳送、A 也即時收到。過程中發現本機 `dotnet run` 的 API process 是舊版 build（在這次 chat 後端 commit 之前就啟動的），聊天相關的兩個 Endpoint 因此回 404，重啟 API process 後恢復正常——這是本機測試環境的問題，不是程式碼問題)
- [x] 5.3 驗證輸入含 `@AI` 的訊息後，畫面上會出現 AI 的回覆訊息（同一組 Playwright 實測：A 傳送含獨立 `@AI` 字詞的訊息，B 也即時看到這則訊息；接著雙方畫面都在數秒內出現一則 `.ai-reply` 樣式的回覆訊息，內容是「AI 目前無法回應，請稍後再試。」——因為本機沒有設定任何 `Ai:Groq:ApiKey`／`Ai:Gemini:ApiKey`，這正好驗證了 spec 的「所有 provider 都失敗時明確告知使用者」這條 Requirement；沒有真的驗證到「AI 給出有意義回覆」這個情境，因為那需要真實的 provider API key，這次的驗證範圍就是走到 provider chain 全部失敗、回退訊息成功出現並廣播給所有參與者為止)

## 6. 端對端驗證

- [x] 6.1 逐一驗證 `specs/ai-chat/spec.md` 的六個 Requirement 全數通過（擁有者/共編者傳訊、非擁有者非共編者被拒絕：`SendChatMessageCommandHandlerTests`/`ChatMessageTests`（Testcontainers）/`GivenNeitherOwnerNorCollaborator...`功能測試涵蓋；`@AI` 觸發、context 包含筆記內容與對話歷史：`AiReplyRequestedDomainEventHandlerTests`/`GenerateAiReplyCommandHandlerTests`涵蓋；備援 provider chain、全部失敗時明確告知：`GivenPrimaryProviderUnavailable...`/`GivenAllProvidersUnavailable...`單元測試 + 上面 5.3 的 Playwright 實測（真的走到全部 provider 失敗的路徑）共同涵蓋。完整 `CoNotes.slnx` 103/103 通過、前端 TypeScript 檢查與 production build 通過)
- [x] 6.2 用兩個不同的測試帳號（一個擁有者、一個共編者）實測聊天室的傳訊、@AI 觸發、無關使用者存取被拒絕（傳訊與 @AI 觸發見上方 5.2/5.3 的 Playwright 實測，兩個帳號都是真的臨時 Keycloak 使用者、真的登入流程；無關使用者存取被拒絕這部分沒有另外用 Playwright 重測，因為後端已經有 `GivenNeitherOwnerNorCollaborator_WhenAccessingChatRoom_ThenRequestRejected`功能測試與 `GivenUnrelatedUser_WhenJoiningChatHubNoteGroupIsRejected` 的 Redis 雙 replica ChatHub 測試真的驗證過同一條規則，屬性上跟前端 UI 無關（沒有專屬的無關使用者畫面要驗證），不再重複實測)
