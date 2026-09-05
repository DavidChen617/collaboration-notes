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

> **後續補上**：先前記錄的「沒有可用的 Docker daemon」已確認是誤判，這個環境其實有 Docker——跑 `dotnet test tests/CoNotes.IntegrationTests` 後 `NoteRepositoryTests`（3.2）、`NoteQueryOwnershipTests`（3.3）全數通過，兩項補打勾。3.3 額外加了 `TestUserContext`（`tests/CoNotes.IntegrationTests/TestUserContext.cs`）取代真正的 `IUserContext`，因為 Query Handler 需要的「目前使用者」在整合測試裡沒有真的 HTTP 請求可以讀。
>
> **後續補上（原本刻意省略，後來照 sample 補回）**：`NoteRepository`/`AppUserRepository` 原本直接用 `IDbConnectionFactory` 每次開新連線，沒有 `AppDbContext`/`UnitOfWork`/`TransactionalDecorator` 這層交易管理，理由是「沒有 task 要求、Domain Event 目前也沒人在聽」。後來決定照 CLAUDE.md 本來就寫明的慣例（Command 端 Repository 用 `AppDbContext`/`UnitOfWork`，Query 端才用 `IDbConnectionFactory`）把這層補齊，程式碼完全比照 `sample/src/TodoApp/Infrastructure/Persistence/AppDbContext.cs`／`UnitOfWork.cs`／`Application/Decorators/TransactionalDecorator.cs`：
> - `Infrastructure/Persistence/AppDbContext.cs`、`UnitOfWork.cs`（新增）
> - `Application/Decorators/TransactionalDecorator.cs`（新增，加了 `Davish.Sendr.Notification` 套件才有 `IPublisher`/`AddSendrNotification()`）
> - `NoteRepository`、`AppUserRepository` 改成注入 `AppDbContext` 而不是 `IDbConnectionFactory`，`AddAsync`/`UpdateAsync`/`DeleteAsync` 都呼叫 `appDbContext.TrackAggregateRoot(...)`
> - `Application/Dependency.cs` 幫所有寫入類 Command（`Upsert/CreateNote/UpdateNote/DeleteNote`）掛上 `.Decorator.With<TransactionalDecorator>()`；Query 維持不掛
> - 用真的本機 Postgres 重跑過全部 16 個 functional test，交易 begin/commit 與 domain event 派發（目前 0 個 listener，`PublishAsync` no-op）都正常

- [x] 3.1 撰寫新的 `golang-migrate` migration，新增 `Note` table（`Id`、`OwnerAppUserId` 外鍵指向 `AppUser`、`Title`、`Content`、`CreatedAt`、`UpdatedAt`），執行後用 `migrate version` 確認套用成功；驗證 down migration 可正確移除該 table
- [x] 3.2 實作 `Note` 的 Dapper Repository（Command 端用），撰寫 Testcontainers 整合測試涵蓋新增/更新/刪除（`NoteRepositoryTests.cs`，已在有 Docker 的環境跑 `dotnet test tests/CoNotes.IntegrationTests` 驗證通過）
- [x] 3.3 實作 `ListNotesQuery`/`GetNoteQuery` 用的 `IDbConnectionFactory` 查詢邏輯，撰寫 Testcontainers 整合測試涵蓋擁有權過濾（`NoteQueryOwnershipTests.cs`，已驗證通過）

## 4. Api

> 加了 `Asp.Versioning.Http`/`.OpenApi`（`Api/Endpoints/v1/Notes/`）跟 `Davish.Endpoints` 的 `IGroupEndpoint`/`IEndpoint<TGroup>` 模式，比照 CLAUDE.md 訂的 `Api/Endpoints/<ApiVersion>/<Feature>/<動詞>.cs` 慣例——這是這個專案第一個「真正的」產品功能 endpoint（先前 setup-infra-and-auth 那個是丟棄式的測試 endpoint，刻意沒上版本化）。

- [x] 4.1 新增建立/更新/刪除/列表/讀取筆記的 Minimal API Endpoint，只做輸入驗證並分派給對應 Command/Query Handler
- [x] 4.2 撰寫 Functional Test（真實 HTTP pipeline），涵蓋五個 Requirement 對應的成功與拒絕情境（含未攜帶驗證、非擁有者存取）（`NoteEndpointTests.cs`，9 個測試，對真的本機 Postgres 全部通過）
- [x] 4.3 撰寫架構測試，驗證 `CoNotes.Domain` 不參考 `CoNotes.Infrastructure`/`CoNotes.Api`（沿用 setup-infra-and-auth 既有的 `LayerDependencyTests`，它是組件層級的檢查，`Note` 加進 `CoNotes.Domain` 後自動涵蓋，不需要新測試）

## 5. Angular 前端

> 這個 change 之前，Angular 專案完全沒有 Keycloak 登入整合——`setup-infra-and-auth` 的 tasks.md 沒有任何前端任務。跟你確認過後，這裡先補上 `keycloak-angular`（`provideKeycloak` + `includeBearerTokenInterceptor`，設定對應 `conotes-realm.json` 的 realm/clientId），才能讓 5.1-5.3 真的叫得動需要驗證身份的 API。
>
> **後續補上**：先前記錄「這個 sandbox 沒有可用的瀏覽器」也是誤判——`npx playwright install chromium` 能裝一顆原生 arm64 的 headless Chromium，實際用它把 Postgres/Keycloak/API/`ng serve` 全部在本機起起來，跑了一次真的登入 → 列表 → 建立 → 編輯 → 刪除的完整流程，5.1-5.3 補打勾。過程中這是本專案第一次讓前後端真的兜起來跑，抓到三個先前不會被單獨測試發現的真實 bug，都已修正：
> 1. `app-config.ts` 寫死的 `API_BASE_URL`（`:5088`）、Keycloak `url`（`:8080`）都跟實際本機設定對不上（API 是 `:5094`，Keycloak 因為前面跟 SigNoz UI 撞 port 已經改成 `:8081`）——已改成正確的值。
> 2. API 完全沒有設定 CORS，瀏覽器直接擋掉所有從 `localhost:4200` 打到 API 的請求——加了 `Configurations/CorsConfiguration.cs`，`Cors:AllowedOrigins` 可設定，`Development` 預設含 `http://localhost:4200`。
> 3. **最關鍵的一個**：`identity/authentication` 的 spec 要求「使用者第一次成功呼叫任何受保護 API」都要建立 `AppUser`，但 upsert 邏輯實際上只掛在拋棄式的 `/api/test/app-user` 這支測試 endpoint 上——一個真實使用者登入後第一次呼叫 `GET /api/v1/notes`，會直接 500（`No AppUser found for Keycloak sub`）。9 個既有的 Notes functional test 全部沒抓到這個問題，因為它們的 `CreateProvisionedClientAsync` 輔助方法本身就會先手動呼叫一次 `/api/test/app-user`，把這個缺口蓋住了。已經把 upsert 移到 JWT Bearer 的 `OnTokenValidated` 事件（任何驗證通過的請求都會經過這裡），並加了一個刻意不預先呼叫該 endpoint 的回歸測試鎖住這個行為。

- [x] 5.1 建立筆記列表畫面，串接列表 API，驗證畫面顯示目前使用者的所有筆記（Playwright 實測：登入後看到「我的筆記」「還沒有任何筆記。」空狀態）
- [x] 5.2 建立筆記編輯畫面，支援建立新筆記與編輯既有筆記內容，驗證儲存後資料確實寫回後端（Playwright 實測：建立「E2E 測試筆記」→ 列表出現 → 編輯標題 → 列表更新為新標題）
- [x] 5.3 在列表畫面加上刪除操作，驗證刪除後該筆記從畫面上消失（Playwright 實測：點刪除後該筆記從列表消失）

## 6. 端對端驗證

- [x] 6.1 逐一驗證 `specs/notes/spec.md` 的五個 Requirement 全數通過（後端 Functional Test 涵蓋 + 前端 Playwright 跑過一次真實登入到刪除的完整流程）
- [ ] 6.2 用兩個不同的 Keycloak 測試帳號，實測跨帳號互相存取對方筆記（讀取／更新／刪除）皆被拒絕（後端 Functional Test 已涵蓋這個情境；用兩個瀏覽器分別登入兩個帳號、在 UI 上實測互相看不到對方筆記，還沒做）
