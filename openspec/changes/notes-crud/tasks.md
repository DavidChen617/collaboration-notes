## 1. Domain

- [ ] 1.1 在 `CoNotes.Domain` 定義 `Note` Aggregate（`Id`、`OwnerAppUserId`、`Title`、`Content`、`CreatedAt`、`UpdatedAt`），封裝「擁有者比對」的不變條件與 `NoteCreated`/`NoteDeleted` Domain Event
- [ ] 1.2 撰寫 `Note` Aggregate 的單元測試（NSubstitute，`GivenXXX_WhenXXX_ThenXXX`），涵蓋建立時發出 `NoteCreated`、刪除時發出 `NoteDeleted`、非擁有者操作時的拒絕邏輯

## 2. Application（Command / Query）

- [ ] 2.1 實作 `CreateNoteCommand` + Handler，驗證單元測試涵蓋成功建立、回傳新 `NoteId`
- [ ] 2.2 實作 `UpdateNoteCommand` + Handler，驗證單元測試涵蓋擁有者更新成功、非擁有者被拒絕
- [ ] 2.3 實作 `DeleteNoteCommand` + Handler，驗證單元測試涵蓋擁有者刪除成功、非擁有者被拒絕
- [ ] 2.4 實作 `ListNotesQuery` + Handler（直接 Dapper 查詢，不經 Domain 層），驗證單元測試涵蓋只回傳呼叫者自己的筆記
- [ ] 2.5 實作 `GetNoteQuery` + Handler，驗證單元測試涵蓋擁有者可查得、非擁有者查詢被拒絕

## 3. Infrastructure

- [ ] 3.1 撰寫新的 `golang-migrate` migration，新增 `Note` table（`Id`、`OwnerAppUserId` 外鍵指向 `AppUser`、`Title`、`Content`、`CreatedAt`、`UpdatedAt`），執行後用 `migrate version` 確認套用成功；驗證 down migration 可正確移除該 table
- [ ] 3.2 實作 `Note` 的 Dapper Repository（Command 端用），撰寫 Testcontainers 整合測試涵蓋新增/更新/刪除
- [ ] 3.3 實作 `ListNotesQuery`/`GetNoteQuery` 用的 `IDbConnectionFactory` 查詢邏輯，撰寫 Testcontainers 整合測試涵蓋擁有權過濾

## 4. Api

- [ ] 4.1 新增建立/更新/刪除/列表/讀取筆記的 Minimal API Endpoint，只做輸入驗證並分派給對應 Command/Query Handler
- [ ] 4.2 撰寫 Functional Test（真實 HTTP pipeline），涵蓋五個 Requirement 對應的成功與拒絕情境（含未攜帶驗證、非擁有者存取）
- [ ] 4.3 撰寫架構測試，驗證 `CoNotes.Domain` 不參考 `CoNotes.Infrastructure`/`CoNotes.Api`

## 5. Angular 前端

- [ ] 5.1 建立筆記列表畫面，串接列表 API，驗證畫面顯示目前使用者的所有筆記
- [ ] 5.2 建立筆記編輯畫面，支援建立新筆記與編輯既有筆記內容，驗證儲存後資料確實寫回後端
- [ ] 5.3 在列表畫面加上刪除操作，驗證刪除後該筆記從畫面上消失

## 6. 端對端驗證

- [ ] 6.1 逐一驗證 `specs/notes/spec.md` 的五個 Requirement 全數通過
- [ ] 6.2 用兩個不同的 Keycloak 測試帳號，實測跨帳號互相存取對方筆記（讀取／更新／刪除）皆被拒絕
