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

- [ ] 5.1 在筆記編輯畫面右下角新增聊天室面板，串接 ChatHub 與歷史訊息 API
- [ ] 5.2 驗證多個使用者同時在同一篇筆記的聊天室裡，彼此的訊息即時同步
- [ ] 5.3 驗證輸入含 `@AI` 的訊息後，畫面上會出現 AI 的回覆訊息

## 6. 端對端驗證

- [ ] 6.1 逐一驗證 `specs/ai-chat/spec.md` 的六個 Requirement 全數通過
- [ ] 6.2 用兩個不同的測試帳號（一個擁有者、一個共編者）實測聊天室的傳訊、@AI 觸發、無關使用者存取被拒絕
