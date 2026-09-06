## 1. Domain

- [x] 1.1 在 `Note` Aggregate 上新增連結相關的不變條件（連結目標必須是同一個擁有者的筆記；解析出的連結集合可以整批替換），並補上 `NoteLinkedTo`／`NoteLinkRemoved` Domain Event 定義（`Note.ResolveLinks(requestedTargetNoteIds, ownedTargetNoteIds)`；`ownedTargetNoteIds` 由呼叫者——Application 層——先查好哪些候選目標真的屬於同一個擁有者，Domain 只做「是否全部落在允許集合內」的驗證與事件計算，不自己碰資料庫）
- [x] 1.2 單元測試（NSubstitute，命名 `GivenXXX_WhenXXX_ThenXXX`）：`GivenLinkTargetOwnedBySameUser_WhenResolveLinks_ThenLinkAccepted`、`GivenLinkTargetOwnedByAnotherUser_WhenResolveLinks_ThenLinkRejected`
- [x] 1.3 單元測試：`GivenNoteContentChanged_WhenLinksResolved_ThenNoteLinkedToEventsRaised`、`GivenLinkRemovedFromContent_WhenLinksResolved_ThenNoteLinkRemovedEventRaised`

## 2. Application

> tasks.md 寫的「`SaveNoteCommand`」對應到既有的 `CreateNoteCommand`／`UpdateNoteCommand`（notes-crud 沒有一個統一叫 `SaveNoteCommand` 的東西）——兩者都會持久化 `Content`，所以連結解析邏輯兩邊都加了，不只 Update。2.4 跟 notes-crud 的 2.4/2.5 一樣，不寫 NSubstitute 單元測試（Dapper `IDbConnection` 不好 mock），改成 3.3 的 Testcontainers 整合測試涵蓋。

- [x] 2.1 實作 `SaveNoteCommand` 的擴充：儲存時解析內容中的連結節點，交給 `Note` Aggregate 驗證後，整批替換連結集合（`NoteLinkContentParser.ExtractLinkedNoteIds` 解析 `data-note-link="{noteId}"`；`CreateNoteCommandHandler`、`UpdateNoteCommandHandler` 都加了：解析 → 查 `FindOwnedNoteIdsAsync` → `note.ResolveLinks(...)`）
- [x] 2.2 單元測試：`GivenValidLinks_WhenSaveNoteCommandHandled_ThenNoteLinkRepositoryCalledWithReplacementSet`（Repository 用 NSubstitute mock）（額外加了 `GivenLinkToNoteNotOwnedByCaller_WhenHandling_ThenIsRejectedAndDoesNotPersist`）
- [x] 2.3 實作 `SearchNotesByTitleQuery`（給自動完成用）與 `GetNoteGraphQuery`（關係圖資料）
- [x] 2.4 單元測試：`GivenKeyword_WhenSearchNotesByTitleQueryHandled_ThenOnlyCallerOwnedNotesReturned`、`GivenUserNotes_WhenGetNoteGraphQueryHandled_ThenNodesAndEdgesMatchOwnership`（跟 notes-crud 2.4/2.5 一樣，改成 3.3 的 Testcontainers 整合測試涵蓋，見 `tests/CoNotes.IntegrationTests/NoteLinkTests.cs`）

## 3. Infrastructure

> 3.1 的 table 名稱用 `note_links`（複數，跟既有的 `notes` 一致），兩個外鍵都加 `on delete cascade`——這樣 3.3 的「刪除筆記後相關連結消失」直接靠資料庫本身處理，不需要在 `DeleteAsync` 額外寫清除連結的邏輯。

- [x] 3.1 撰寫 migration 新增 `NoteLink` table（`SourceNoteId`、`TargetNoteId` 皆為 `Note` 外鍵，兩者組合唯一），執行後用 `migrate version` 確認套用成功；驗證 down 可正確移除
- [x] 3.2 實作 `NoteLink` 的 Dapper Repository：整批刪除＋重新寫入包在同一交易內（`NoteRepository.ReplaceLinksAsync`，`AddAsync`/`UpdateAsync` 都會呼叫；新增 `FindOwnedNoteIdsAsync` 供 Application 層驗證連結目標擁有權）
- [x] 3.3 Testcontainers 整合測試：`GivenNoteWithLinks_WhenSaved_ThenNoteLinkTableMatchesContent`、`GivenNoteDeleted_WhenQueried_ThenNoLinkRowsRemain`（`tests/CoNotes.IntegrationTests/NoteLinkTests.cs`，跟 2.4 的兩個查詢測試一起，全部通過）

## 4. Api

- [x] 4.1 新增依標題搜尋自己筆記的 endpoint（給自動完成用）（`GET /api/v1/notes/search?keyword=...`）
- [x] 4.2 新增取得關係圖資料的 endpoint（`GET /api/v1/notes/graph`；跟既有的 `GET /api/v1/notes/{noteId}` 共用同一個路由群組，實測過 ASP.NET Core 的路由比對會優先選字面量路由 `search`/`graph`，不會被 `{noteId}` 參數路由攔截）
- [x] 4.3 Functional Test：`GivenAuthenticatedUser_WhenSearchNotesByTitle_ThenOnlyOwnNotesReturned`、`GivenAuthenticatedUser_WhenGetNoteGraph_ThenOnlyOwnDataReturned`
- [x] 4.4 Functional Test：`GivenLinkToOtherUsersNote_WhenSaveNote_ThenRequestRejected`
- [x] 4.5 架構測試：驗證 `Domain` 專案不參考 `Infrastructure`／`Api`（沿用既有的 `LayerDependencyTests`，組件層級檢查，新增的型別都在既有專案內，自動涵蓋，不需要新測試）

## 5. 前端編輯器

- [ ] 5.1 導入 Tiptap 取代目前的筆記編輯器，驗證既有的筆記內容仍可正常載入與編輯
- [ ] 5.2 實作以 `[[` 觸發的自動完成選單，串接標題搜尋 API，驗證輸入 `[[` 後能看到自己的筆記清單；若雙字元觸發無法乾淨支援，改用單一 `[` 觸發並記錄此決定
- [ ] 5.3 實作選定筆記後插入連結節點，畫面顯示標題、底層存識別碼，驗證儲存後重新載入內容，連結顯示正確
- [ ] 5.4 驗證被連結筆記標題更改後，重新開啟含有該連結的筆記，顯示的標題會自動更新

## 6. 前端關係圖

- [ ] 6.1 導入 Cytoscape.js，建立關係圖檢視畫面，串接關係圖資料 API
- [ ] 6.2 驗證畫面正確顯示所有筆記節點與連結邊，且點選節點可以跳轉到對應的筆記編輯畫面

## 7. 端對端驗證

- [ ] 7.1 逐一驗證 `specs/note-linking/spec.md` 的五個 Requirement 全數通過
- [ ] 7.2 建立多篇互相連結的筆記，刪除其中一篇後確認關係圖與相關連結都正確更新
