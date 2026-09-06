## 1. Domain

> `ShareToken` 型別是 `ShareLinkToken`(Domain Value Object, 包一個非空字串, 建構子擋 null/空字串)——之前的版本讓 `Note.GenerateShareLink` 自己用 `Guid.NewGuid()` 產生 token, 後來討論後改掉: 「怎麼產生一個不可猜測的值」是技術細節, 不該由 Domain 決定; Domain 只接收已經產生好的 `ShareLinkToken`、驗證擁有權、記錄狀態、觸發事件。方法也從 `GenerateShareLink(requestingAppUserId)`(自己生成並回傳 token)改名為 `SetShareLink(requestingAppUserId, shareToken)`(純狀態轉換, 不回傳值);`Guid.NewGuid().ToString()` 的產生動作搬到 Application 層的 Command Handler。`RevokeShareLink` 現在只清掉舊 token、觸發 `NoteShareLinkRevoked`, 不再自己生成替代 token——要立刻換發新連結(對應 spec 的「撤銷後 SHALL 產生新的有效連結」), 由 `RevokeShareLinkCommandHandler` 呼叫完 `RevokeShareLink` 後, 自己生成新 token 再呼叫一次 `SetShareLink`, 兩次呼叫作用在同一個記憶體中的 aggregate 實例, 最後一次 `UpdateAsync` 落地。`notes.share_token` 欄位型別也從 `uuid` 改成 `text`, 呼應「Domain 不假設 token 是 GUID 格式」。新增 `Note.IsAccessibleBy(appUserId)`(擁有者或共編者), `Update` 的存取檢查改用這個方法。
>
> **後續補上**:上面提到「尚未定案」的擁有權檢查搬遷, 後來決定要做——`Update`／`Delete`／`SetShareLink`／`RevokeShareLink`／`RemoveCollaborator` 這 5 個方法(不含 `JoinViaShareLink`, 見下)的擁有權檢查都搬到 Application 層的對應 Command Handler, 呼叫 Domain 方法前先用 `note.IsAccessibleBy(...)`(Update)或 `note.IsOwnedBy(...)`(其餘 4 個)守門, 檢查失敗就直接回原本一樣的 Error(錯誤代碼與訊息不變, 只是拋出的位置從 Domain 換成 Application, 外部行為不變)。Domain 方法本身因此變成純狀態轉換, `Update`/`Delete`/`SetShareLink`/`RevokeShareLink` 都改回傳 `void`(已經沒有失敗路徑), `RemoveCollaborator` 仍回傳 `Result`(唯一的失敗路徑是「這個人本來就不是共編者」, 這是真正的業務規則, 不是授權問題, 留在 Domain)。`JoinViaShareLink` 刻意不動:它驗證的是「這個 token 是不是這篇筆記目前有效的 `ShareToken`」, 這是 Aggregate 自己狀態的不變條件, 不是「呼叫者是誰」這種可以外移的存取控制, 所以繼續留在 Domain。新增 `Note.IsOwnedBy(appUserId)` 搭配既有的 `IsAccessibleBy(appUserId)`, 供 Application 層在呼叫前守門用。

- [x] 1.1 在 `CoNotes.Domain` 的 `Note` Aggregate 上新增分享連結／共編者名單相關的不變條件（產生/撤銷 `ShareToken`、加入/移除共編者，且只有擁有者能操作),補上 `NoteShareLinkGenerated`／`NoteShareLinkRevoked`／`NoteCollaboratorJoined`／`NoteCollaboratorRemoved` Domain Event 定義（擁有權檢查後來搬到 Application 層, 見上方說明；Domain 現在只透過 `IsOwnedBy`/`IsAccessibleBy` 這兩個 predicate 供外部查詢, 自己不再拒絕呼叫）
- [x] 1.2 單元測試（NSubstitute，`GivenXXX_WhenXXX_ThenXXX`）：`GivenOwner_WhenGeneratingShareLink_ThenShareTokenCreatedAndEventRaised`、`GivenNonOwner_WhenGeneratingShareLink_ThenRejected`（Domain 層的版本現在是 `GivenValidInput_WhenSettingShareLink_ThenShareTokenStoredAndEventRaised`；非擁有者被拒絕的情境搬到 `GenerateShareLinkCommandHandlerTests` 驗證)
- [x] 1.3 單元測試：`GivenActiveShareLink_WhenRevoked_ThenTokenInvalidatedButExistingCollaboratorsUnaffected`
- [x] 1.4 單元測試：`GivenValidShareToken_WhenUserJoins_ThenAddedToCollaboratorListAndEventRaised`、`GivenOwner_WhenRemovingCollaborator_ThenRemovedAndEventRaised`（額外加了重複加入、無效 token 被拒絕、非共編者移除被拒絕這幾個情境；「非擁有者操作被拒絕」的情境現在都在對應的 Application Command Handler 測試裡)

## 2. Application（Command / Query）

> `JoinNoteViaShareLinkCommand` 靠新增的 `INoteRepository.GetByShareTokenAsync` 找到對應筆記(呼叫者不知道 NoteId, 只有 token)。`UpdateNoteCommand` 完全不用改——它本來就呼叫 `note.Update(...)`, 而 1.1 已經把 `Note.Update` 的存取檢查換成 `IsAccessibleBy`(擁有者或共編者), Command Handler 本身零異動就自動享有這個放寬。`ListNotesQuery`／`GetNoteQuery` 是純 SQL Read Model, 改成 `OwnerAppUserId = @user OR EXISTS(SELECT 1 FROM note_collaborators ...)` 條件(design.md decision 7)。

- [x] 2.1 實作 `GenerateShareLinkCommand` + Handler，驗證單元測試涵蓋回傳的分享連結帶有唯一、不可猜測的 token
- [x] 2.2 實作 `RevokeShareLinkCommand` + Handler，驗證單元測試涵蓋撤銷後產生新連結、既有共編者不受影響
- [x] 2.3 實作 `JoinNoteViaShareLinkCommand` + Handler，驗證單元測試涵蓋不同使用者用同一條連結都能加入、重複開啟不產生重複記錄
- [x] 2.4 實作 `RemoveCollaboratorCommand` + Handler，驗證單元測試涵蓋移除後該使用者無法再存取
- [x] 2.5 更新既有 `ListNotesQuery`／`GetNoteQuery`／`UpdateNoteCommand` 的存取邏輯，改為「擁有者或共編者」皆可通過，驗證單元測試涵蓋共編者可讀取/更新、無關使用者被拒絕（`UpdateNoteCommand` 見上方說明；`ListNotesQuery`/`GetNoteQuery` 的單元測試因為是純 Dapper SQL、跟 notes-crud 的既有慣例一樣改成 3.6 的 Testcontainers 整合測試涵蓋，不寫 NSubstitute 測試）

## 3. Infrastructure

> `note_updates`/`note_snapshots` 都用 `(note_id, sequence_number)` 當主鍵、存 opaque 的 `bytea`——後端完全不理解 Yjs binary 內容, 只負責依序存取/回放, 合併/重建邏輯留在前端用 Yjs 自己的 API 做(design.md decision 1)。`note_snapshots` 不是只留最新一份, 而是每次壓縮都新增一列(不覆蓋舊快照), 這樣「快照 + 之後的更新紀錄」才能疊出任一個過去時間點, 真正被清除的只有已經被某個快照涵蓋的 `note_updates` 舊列。

- [x] 3.1 撰寫 migration 新增 `NoteCollaborator` table（`NoteId`、`AppUserId` 皆為外鍵，組合唯一）與 `Note` 的 `ShareToken` 欄位（`share_token` 欄位加了 partial unique index, 允許多列是 null、但已產生的 token 不能重複）
- [x] 3.2 撰寫 migration 新增筆記的 Yjs 更新紀錄 table（append-only，含序號）與快照 table；驗證以上 migration 的 down 都能正確移除對應 table／欄位（`migrate down 2` 之後 `migrate`(up)重新套用, 全部正常）
> 3.5 的壓縮:「把目前狀態合併成快照」需要先合併 Yjs binary update, 但 design.md decision 1 明講 server 不理解 Yjs 的合併語義。跟使用者確認過, 採用「client 主動上傳快照」:`NoteCollabHub.SendUpdateAsync` 每次 append 完更新紀錄後, 若累積筆數超過門檻(`NoteEditHistoryStore.CompactionThreshold`, 目前 200), 會透過 `Clients.Caller.SendAsync("SnapshotRequested", noteId)` 請發送這次 update 的那個 client 用 Yjs 自己的 `Y.encodeStateAsUpdate(doc)` 算出完整合併狀態, 再呼叫新的 `SaveSnapshotAsync(noteId, snapshot)` 上傳; server 端只負責存起來、刪除已被涵蓋的舊 `note_updates` 列, 全程不碰 Yjs 的二進位內容語義。前端這段串接見 5.1/5.4。

- [x] 3.3 建立獨立的 SignalR Hub，以 `NoteId` 當 Group，加入前執行跟 REST API 一致的存取檢查（擁有者或共編者）（`NoteCollabHub.JoinNoteAsync`，`[Authorize]` + `Note.IsAccessibleBy`；JWT 透過 query string `access_token` 帶入，因為瀏覽器的 WebSocket 交握無法帶自訂 header，見 `AuthenticationConfiguration.OnMessageReceived`）
- [x] 3.4 實作接收 Yjs update 後直接轉發給同一 Group 其他連線的邏輯，並將 update 附加寫入更新紀錄 table（`NoteCollabHub.SendUpdateAsync` → `Clients.OthersInGroup` 轉發 + `INoteEditHistoryStore.AppendUpdateAsync` 寫入；序號用 `coalesce(max(sequence_number),0)+1` 算，玩具規模下接受這裡有極小的並行競爭風險，未加額外鎖）
- [x] 3.5 實作存檔時檢查更新紀錄筆數，超過門檻就合併成快照、清除已合併的舊更新紀錄（見上方說明；`NoteEditHistoryStore.SaveSnapshotAsync` 用一個 DB transaction 包住「寫入快照 + 刪除被涵蓋的舊 update 列」）
- [x] 3.6 Testcontainers 整合測試：`GivenShareTokenAndCollaboratorRows_WhenQueried_ThenAccessCheckMatchesOwnerOrCollaborator`、`GivenSnapshotAndSubsequentUpdates_WhenReplayed_ThenReconstructsContentAtGivenPoint`（`tests/CoNotes.IntegrationTests/NoteCollaborationTests.cs`；過程中發現並修正一個真的 bug：`AppendUpdateAsync` 原本只看 `note_updates` 表算下一個序號，壓縮把該表清空後序號會從頭算起、跟 `note_snapshots` 記錄的 cutoff 撞號，已改成同時比較兩張表的最大序號）
- [x] 3.7 驗證 SignalR 透過既有 Redis backplane 運作（啟動兩個 API replica，確認不同 replica 上的連線仍能收到彼此的 update）（`tests/CoNotes.IntegrationTests/NoteCollabHubReplicaTests.cs`：真的用 `WebApplicationFactory<Program>` 啟動兩個獨立 host、都接本機 `docker-compose` 的 Redis，連到 replica 1 的 SignalR 連線送出 update，連到 replica 2 的連線確實收到，證明轉發真的走 Redis、不是侷限在單一 process 記憶體內。放進 `CoNotes.IntegrationTests`（而不是 `CoNotes.FunctionalTests`）是因為 `Davish.Result` 的 `ResultHttpOptions` 是 process-wide、只能設定一次——`FunctionalTests` 專案的其他測試已經啟動過一個 host 並處理過請求，這裡再啟動兩個新 host 會直接噴 `ResultHttpOptionsLockedException`；`IntegrationTests` 裡其他測試都只透過 `ISender` 直接呼叫、從未真的送出 HTTP request，所以不會撞到這個鎖。另外因為 `IntegrationTestWebAppFactory` 把 `IUserContext` 換成固定回傳值的 `TestUserContext`（給其他不經 HTTP 的測試用），這裡兩個 replica 各自换回真正的 `UserContext`，才能讓兩邊都從同一個 JWT 解析出同一個 AppUserId）

## 4. Api

> Endpoint 路由：`POST /notes/{noteId}/share-link`(產生)、`POST /notes/{noteId}/share-link/revoke`(撤銷並換發新連結)、`POST /notes/share-link/{shareToken}/join`(加入共編，不掛在 `{noteId}` 底下是因為呼叫者不知道 noteId，只有 token)、`DELETE /notes/{noteId}/collaborators/{collaboratorAppUserId}`(移除共編者)。4.2 原本寫「回 403」，但這個專案從 notes-crud 開始，所有「不是擁有者」的拒絕都是 `ErrorType.BadRequest` → 400（既有的 `GivenNonOwner_WhenUpdatingSomeoneElsesNote...`／`GivenNonOwner_WhenDeletingSomeoneElsesNote...` 都是驗證 400），這裡沿用同一個慣例改成 400，不然同一種「不是擁有者」的錯誤在 API 裡會有兩種狀態碼。

- [x] 4.1 新增產生/撤銷分享連結、移除共編者的 Minimal API Endpoint（`GenerateShareLink.cs`／`RevokeShareLink.cs`／`JoinNoteViaShareLink.cs`／`RemoveCollaborator.cs`）
- [x] 4.2 功能測試：`GivenOwner_WhenGeneratingOrRevokingShareLink_ThenSucceeds`、`GivenCollaborator_WhenGeneratingOrRevokingShareLinkOrRemovingCollaborator_ThenIsRejected`（見上方說明，回 400 不是 403）
- [x] 4.3 功能測試：`GivenAuthenticatedUser_WhenOpeningValidShareLink_ThenAddedAsCollaborator`（額外加了 `GivenInvalidShareToken_WhenJoining_ThenIsRejected`）
- [x] 4.4 功能測試：`GivenCollaborator_WhenListingOrReadingOrUpdatingNote_ThenSucceeds`、`GivenUnrelatedUser_WhenAccessingNote_ThenRejected`（涵蓋 `specs/notes/spec.md` 這次修改的三個 Requirement；額外加了 `GivenOwner_WhenRemovingCollaborator_ThenCollaboratorCanNoLongerAccessTheNote`，都在 `tests/CoNotes.FunctionalTests/NoteCollabEndpointTests.cs`）
- [x] 4.5 架構測試：驗證 `CoNotes.Domain` 不參考 `CoNotes.Infrastructure`／`CoNotes.Api`（沿用既有的 `LayerDependencyTests`，組件層級檢查，新增的型別都在既有專案內，自動涵蓋，不需要新測試）

## 5. 前端整合

- [ ] 5.1 在 Tiptap 編輯器加上官方的 Yjs 協作擴充套件，串接 SignalR 當傳輸層
- [ ] 5.2 驗證兩個瀏覽器分頁同時編輯同一篇筆記，彼此的變更即時同步、沒有內容被覆蓋遺失
- [ ] 5.3 新增「產生/撤銷分享連結」與「移除共編者」的 UI，串接對應 API
- [ ] 5.4 新增筆記編輯歷史的檢視功能，可以選擇過去時間點回放內容

## 6. 端對端驗證

- [ ] 6.1 逐一驗證 `specs/collab-editing/spec.md` 的六個 Requirement 全數通過
- [ ] 6.2 逐一驗證 `specs/notes/spec.md` 這次修改的三個 Requirement（列表、讀取、更新）全數通過，且刪除/建立的行為未受影響
