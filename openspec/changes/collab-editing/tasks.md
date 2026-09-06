## 1. Domain

> `ShareToken` 用 `Guid.NewGuid()`(v4 全亂數)產生, 不是這個專案 Aggregate ID 慣用的 `Guid.CreateVersion7()`——v7 會編碼時間戳記, 拿來當安全憑證會洩漏建立時間、也減少可用亂數位元, 細節見 `Note.GenerateShareLink` 的 doc comment。`RevokeShareLink` 撤銷舊 token 後會立刻產生新 token(對應 spec 的「撤銷後 SHALL 產生新的有效連結」), 一次呼叫觸發 `NoteShareLinkRevoked` + `NoteShareLinkGenerated` 兩個事件。新增 `Note.IsAccessibleBy(appUserId)`(擁有者或共編者), `Update` 的存取檢查改用這個方法。

- [x] 1.1 在 `CoNotes.Domain` 的 `Note` Aggregate 上新增分享連結／共編者名單相關的不變條件（產生/撤銷 `ShareToken`、加入/移除共編者，且只有擁有者能操作），補上 `NoteShareLinkGenerated`／`NoteShareLinkRevoked`／`NoteCollaboratorJoined`／`NoteCollaboratorRemoved` Domain Event 定義
- [x] 1.2 單元測試（NSubstitute，`GivenXXX_WhenXXX_ThenXXX`）：`GivenOwner_WhenGeneratingShareLink_ThenShareTokenCreatedAndEventRaised`、`GivenNonOwner_WhenGeneratingShareLink_ThenRejected`
- [x] 1.3 單元測試：`GivenActiveShareLink_WhenRevoked_ThenTokenInvalidatedButExistingCollaboratorsUnaffected`
- [x] 1.4 單元測試：`GivenValidShareToken_WhenUserJoins_ThenAddedToCollaboratorListAndEventRaised`、`GivenOwner_WhenRemovingCollaborator_ThenRemovedAndEventRaised`（額外加了重複加入、非擁有者移除被拒絕、共編者可更新這幾個情境）

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
- [ ] 3.3 建立獨立的 SignalR Hub，以 `NoteId` 當 Group，加入前執行跟 REST API 一致的存取檢查（擁有者或共編者）
- [ ] 3.4 實作接收 Yjs update 後直接轉發給同一 Group 其他連線的邏輯，並將 update 附加寫入更新紀錄 table
- [ ] 3.5 實作存檔時檢查更新紀錄筆數，超過門檻就合併成快照、清除已合併的舊更新紀錄
- [ ] 3.6 Testcontainers 整合測試：`GivenShareTokenAndCollaboratorRows_WhenQueried_ThenAccessCheckMatchesOwnerOrCollaborator`、`GivenSnapshotAndSubsequentUpdates_WhenReplayed_ThenReconstructsContentAtGivenPoint`
- [ ] 3.7 驗證 SignalR 透過既有 Redis backplane 運作（啟動兩個 API replica，確認不同 replica 上的連線仍能收到彼此的 update）

## 4. Api

- [ ] 4.1 新增產生/撤銷分享連結、移除共編者的 Minimal API Endpoint
- [ ] 4.2 功能測試：`GivenOwner_WhenGeneratingOrRevokingShareLink_ThenSucceeds`、`GivenCollaborator_WhenGeneratingOrRevokingShareLinkOrRemovingCollaborator_ThenReturns403`
- [ ] 4.3 功能測試：`GivenAuthenticatedUser_WhenOpeningValidShareLink_ThenAddedAsCollaborator`
- [ ] 4.4 功能測試：`GivenCollaborator_WhenListingOrReadingOrUpdatingNote_ThenSucceeds`、`GivenUnrelatedUser_WhenAccessingNote_ThenRejected`（涵蓋 `specs/notes/spec.md` 這次修改的三個 Requirement）
- [ ] 4.5 架構測試：驗證 `CoNotes.Domain` 不參考 `CoNotes.Infrastructure`／`CoNotes.Api`

## 5. 前端整合

- [ ] 5.1 在 Tiptap 編輯器加上官方的 Yjs 協作擴充套件，串接 SignalR 當傳輸層
- [ ] 5.2 驗證兩個瀏覽器分頁同時編輯同一篇筆記，彼此的變更即時同步、沒有內容被覆蓋遺失
- [ ] 5.3 新增「產生/撤銷分享連結」與「移除共編者」的 UI，串接對應 API
- [ ] 5.4 新增筆記編輯歷史的檢視功能，可以選擇過去時間點回放內容

## 6. 端對端驗證

- [ ] 6.1 逐一驗證 `specs/collab-editing/spec.md` 的六個 Requirement 全數通過
- [ ] 6.2 逐一驗證 `specs/notes/spec.md` 這次修改的三個 Requirement（列表、讀取、更新）全數通過，且刪除/建立的行為未受影響
