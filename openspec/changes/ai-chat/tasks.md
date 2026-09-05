## 1. Domain

- [ ] 1.1 在 `CoNotes.Domain` 定義 `ChatMessage` Aggregate Root（`Id`、`NoteId`、`AuthorAppUserId` 可為 null、`IsAiReply`、`Content`、`CreatedAt`），封裝「訊息內容含獨立 `@AI` 字詞」的判斷邏輯，建立時視情況發出 `ChatMessageSent`／`AiReplyRequested` Domain Event
- [ ] 1.2 單元測試（`GivenXXX_WhenXXX_ThenXXX`）：`GivenMessageWithStandaloneAiMention_WhenChatMessageCreated_ThenRaisesAiReplyRequested`、`GivenMessageWithAiSubstringInsideEmail_WhenChatMessageCreated_ThenDoesNotRaiseAiReplyRequested`、`GivenPlainHumanMessage_WhenChatMessageCreated_ThenRaisesChatMessageSentOnly`
- [ ] 1.3 定義 `IChatMessageRepository`（`AddAsync`、`GetRecentByNoteIdAsync`）與 `IAiChatProvider`（`TryGetReplyAsync(context) -> Reply?`）介面，供 Application 層依賴、Infrastructure 層實作

## 2. Application（Command / Query）

- [ ] 2.1 實作 `SendChatMessageCommand` + Handler：驗證呼叫者為擁有者或共編者、儲存訊息、透過 ChatHub 廣播；單元測試（mock `IChatMessageRepository`）涵蓋擁有者/共編者傳送成功、非擁有者非共編者被拒絕
- [ ] 2.2 實作 `GenerateAiReplyCommand` + Handler（由 `AiReplyRequested` 觸發的背景流程）：組合「筆記目前內容（截斷過長內容）」與「最近對話歷史」當 context，依序呼叫 `IAiChatProvider` chain
- [ ] 2.3 單元測試（mock 多個 `IAiChatProvider`，NSubstitute）：`GivenPrimaryProviderUnavailable_WhenGeneratingAiReply_ThenFallbackProviderIsUsed`、`GivenAllProvidersUnavailable_WhenGeneratingAiReply_ThenAiReplyFailedRaisedAndFallbackMessageStored`、`GivenAnyProviderSucceeds_WhenGeneratingAiReply_ThenAiReplyGeneratedRaisedWithReplyContent`
- [ ] 2.4 單元測試：`GivenNoteContentExceedsLengthLimit_WhenBuildingAiContext_ThenContentIsTruncated`
- [ ] 2.5 實作 `GetChatHistoryQuery` + Handler（直接 Dapper 查詢，不經 Domain 層），單元測試涵蓋只有擁有者或共編者能取得歷史訊息

## 3. Infrastructure

- [ ] 3.1 撰寫 migration 新增 `ChatMessage` table（`NoteId` 外鍵、`AuthorAppUserId` 可為 null、`IsAiReply` 布林、`Content`、`CreatedAt`），執行後用 `migrate version` 確認套用成功；驗證 down 可正確移除
- [ ] 3.2 實作 `ChatMessage` 的 Dapper Repository，Testcontainers 整合測試：`GivenChatMessageSaved_WhenQueriedByNoteId_ThenReturnsMessageInOrder`
- [ ] 3.3 定義至少兩個 `IAiChatProvider` 的免費額度 provider adapter 實作
- [ ] 3.4 建立獨立的 SignalR ChatHub，以 `NoteId` 當 Group，加入前執行跟 Notes API 一致的存取檢查（擁有者或共編者），沿用既有 Redis backplane

## 4. Api

- [ ] 4.1 新增傳送訊息、取得聊天歷史的 Minimal API Endpoint / Hub method，只做輸入驗證並分派給對應 Command/Query Handler
- [ ] 4.2 Functional Test：`GivenOwnerOrCollaborator_WhenSendingChatMessage_ThenMessageStoredAndBroadcast`
- [ ] 4.3 Functional Test：`GivenNeitherOwnerNorCollaborator_WhenAccessingChatRoom_ThenRequestRejected`
- [ ] 4.4 Functional Test：`GivenMessageWithAiMention_WhenProcessed_ThenAiReplyEventuallyAppearsInChatRoom`
- [ ] 4.5 架構測試：驗證 `CoNotes.Domain` 不參考 `CoNotes.Infrastructure`/`CoNotes.Api`

## 5. 前端聊天室 UI

- [ ] 5.1 在筆記編輯畫面右下角新增聊天室面板，串接 ChatHub 與歷史訊息 API
- [ ] 5.2 驗證多個使用者同時在同一篇筆記的聊天室裡，彼此的訊息即時同步
- [ ] 5.3 驗證輸入含 `@AI` 的訊息後，畫面上會出現 AI 的回覆訊息

## 6. 端對端驗證

- [ ] 6.1 逐一驗證 `specs/ai-chat/spec.md` 的六個 Requirement 全數通過
- [ ] 6.2 用兩個不同的測試帳號（一個擁有者、一個共編者）實測聊天室的傳訊、@AI 觸發、無關使用者存取被拒絕
