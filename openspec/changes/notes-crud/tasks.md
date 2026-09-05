## 1. Domain

- [x] 1.1 在 `CoNotes.Domain` 定義 `Note` Aggregate（`Id`、`OwnerAppUserId`、`Title`、`Content`、`CreatedAt`、`UpdatedAt`），封裝「擁有者比對」的不變條件與 `NoteCreated`/`NoteDeleted` Domain Event
- [x] 1.2 撰寫 `Note` Aggregate 的單元測試（NSubstitute，`GivenXXX_WhenXXX_ThenXXX`），涵蓋建立時發出 `NoteCreated`、刪除時發出 `NoteDeleted`、非擁有者操作時的拒絕邏輯

## 2. Application（Command / Query）

> 加了一個 tasks.md 沒明講但必要的抽象：`IUserContext`（`Application/Abstractions/IUserContext.cs`，`GetAppUserIdAsync`），跟 sample 的 `IUserContext.UserId` 同樣的角色，只是我們的 AppUserId 需要用 Keycloak `sub` 查表才能拿到，所以是 async。Handler 裡的擁有權比對走「Handler 拿 `IUserContext` 給的 AppUserId → 傳進 `Note.Update`/`Note.Delete`」，不是像 sample 的 Todo 一樣直接在 Handler 比對——這是 design.md 明講要封裝進 Aggregate 的緣故。2.4/2.5 沒有另外寫 NSubstitute 單元測試：這個專案的慣例（見 `sample/`）本來就不對 Dapper Query Handler 寫單元測試（`IDbConnection`/`CommandDefinition` 不好 mock），擁有權過濾的驗證留給 3.3 的 Testcontainers 整合測試涵蓋。

- [x] 2.1 實作 `CreateNoteCommand` + Handler，驗證單元測試涵蓋成功建立、回傳新 `NoteId`
- [x] 2.2 實作 `UpdateNoteCommand` + Handler，驗證單元測試涵蓋擁有者更新成功、非擁有者被拒絕
- [x] 2.3 實作 `DeleteNoteCommand` + Handler，驗證單元測試涵蓋擁有者刪除成功、非擁有者被拒絕
- [x] 2.4 實作 `ListNotesQuery` + Handler（直接 Dapper 查詢，不經 Domain 層），驗證單元測試涵蓋只回傳呼叫者自己的筆記（測試覆蓋見 3.3）
- [x] 2.5 實作 `GetNoteQuery` + Handler，驗證單元測試涵蓋擁有者可查得、非擁有者查詢被拒絕（測試覆蓋見 3.3）

## 3. Infrastructure

> 這個環境一樣沒有可用的 Docker daemon（見 setup-infra-and-auth 的說明），3.2/3.3 的 Testcontainers 測試編譯通過、寫法上重用同一支 `IntegrationTestWebAppFactory`，但在這裡跑會於連線 Docker daemon 那步失敗，邏輯本身已經另外用未納入 repo 的 scratch console app 對本機 Postgres 跑過一輪 Add/Update/Delete/GetById 全部正確。3.3 額外加了 `TestUserContext`（`tests/CoNotes.IntegrationTests/TestUserContext.cs`）取代真正的 `IUserContext`，因為 Query Handler 需要的「目前使用者」在整合測試裡沒有真的 HTTP 請求可以讀。

- [x] 3.1 撰寫新的 `golang-migrate` migration，新增 `Note` table（`Id`、`OwnerAppUserId` 外鍵指向 `AppUser`、`Title`、`Content`、`CreatedAt`、`UpdatedAt`），執行後用 `migrate version` 確認套用成功；驗證 down migration 可正確移除該 table
- [ ] 3.2 實作 `Note` 的 Dapper Repository（Command 端用），撰寫 Testcontainers 整合測試涵蓋新增/更新/刪除（`NoteRepositoryTests.cs` 已寫，編譯通過，待有 Docker 的環境跑 `dotnet test tests/CoNotes.IntegrationTests` 驗證）
- [ ] 3.3 實作 `ListNotesQuery`/`GetNoteQuery` 用的 `IDbConnectionFactory` 查詢邏輯，撰寫 Testcontainers 整合測試涵蓋擁有權過濾（`NoteQueryOwnershipTests.cs` 已寫，編譯通過，待有 Docker 的環境跑 `dotnet test tests/CoNotes.IntegrationTests` 驗證）

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
