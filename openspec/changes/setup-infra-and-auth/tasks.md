## 1. Domain（`CoNotes.Domain`）

- [x] 1.1 定義 `AppUser` Aggregate Root（`Id`、`KeycloakSub`、`CreatedAt`），提供建立新 `AppUser` 的工廠方法，建立時記錄 `AppUserProvisioned` Domain Event
- [x] 1.2 單元測試（NSubstitute 不需要，純 Domain 邏輯）：`GivenNewKeycloakSub_WhenCreatingAppUser_ThenRaisesAppUserProvisionedEvent`
- [x] 1.3 定義 `IAppUserRepository` 介面（`FindByKeycloakSubAsync`、`AddAsync`），供 Application 層依賴、Infrastructure 層實作

## 2. Application（`CoNotes.Application`）

- [x] 2.1 實作 `UpsertAppUserCommand` + `UpsertAppUserCommandHandler`：查詢 `IAppUserRepository` 是否已有對應 `AppUser`，沒有就建立新的並寫入，已有就直接回傳既有記錄
- [x] 2.2 單元測試（mock `IAppUserRepository`）：`GivenAppUserDoesNotExist_WhenHandlingUpsertAppUserCommand_ThenCreatesAndPersistsNewAppUser`
- [x] 2.3 單元測試（mock `IAppUserRepository`）：`GivenAppUserAlreadyExists_WhenHandlingUpsertAppUserCommand_ThenReturnsExistingAppUserWithoutDuplication`

## 3. Infrastructure（`CoNotes.Infrastructure` + 叢集基礎設施）

> 開發環境備註：這裡沒有可用的 Docker daemon（容器啟動後連 overlayfs mount 都沒有權限）、也沒有 kubectl/k8s cluster。3.4／3.5 改用本機直接 apt 裝的 PostgreSQL 16 手動驗證（`CREATE ROLE`/`CREATE DATABASE`、`migrate up`/`down`/`version`），驗證的是 migration script 與 repository SQL 本身正確，不是實際 k8s 上的 Postgres。3.2 需要的 Testcontainers 因此無法執行，改用一支未納入 repo 的 scratch console app 手動打過一次 `AppUserRepository`（insert + lookup）確認邏輯正確，之後有 docker 的環境應把 3.2 的 Testcontainers 測試按原計畫補上。3.3/3.6/3.9/3.10 之後只寫 manifest，不勾選「部署後確認」的部分。

- [x] 3.1 實作 `IAppUserRepository` 的 Dapper 版本（`AppUserRepository`），透過 `IDbConnectionFactory` 取得連線
- [ ] 3.2 整合測試（Testcontainers 起真的 Postgres）：`GivenPostgresDatabase_WhenUpsertingSameKeycloakSubTwice_ThenSecondCallReturnsSameAppUserRow`（阻塞：此環境沒有可用的 Docker daemon，見下方說明）
- [ ] 3.3 撰寫 Postgres 的 k8s manifest（Deployment/StatefulSet + Service + PVC），部署後用 `kubectl get pods` 確認 pod 為 Running 狀態
- [x] 3.4 建立 `app`、`keycloak` 兩個 database，並確認可分別用對應帳號連線成功
- [x] 3.5 安裝 `golang-migrate` CLI，撰寫第一版 migration 建立 `AppUser` table，執行後用 `migrate version` 確認已套用到最新版本；驗證 down/up 都能重複執行不報錯
- [ ] 3.6 撰寫 Keycloak 的 k8s manifest（Deployment + Service），指向 `keycloak` database，部署後確認 pod Running 且能連上該 database
- [ ] 3.7 手動建立 Realm 與一個開啟 PKCE 的 public client，設定合法的 redirect URI；匯出成 realm-export JSON 存進 repo，並驗證用此檔案可重新匯入出一致的 Realm
- [ ] 3.8 手動走一次 Keycloak 登入頁面完成登入，確認可取得 access token
- [ ] 3.9 撰寫 SigNoz 的 k8s manifest，部署後確認可以開啟 SigNoz 的 UI
- [ ] 3.10 部署 ingress-nginx controller，確認其 Service 已取得可用的內部位址；建立 Ingress 規則，把 `api.<domain>` 導向 API Service、`auth.<domain>` 導向 Keycloak Service
- [ ] 3.11 更新 Cloudflare Tunnel 設定，將目標指向 nginx 的 Service
- [x] 3.12 架構測試（不依賴套件，自行檢查組件參照）：`GivenDomainAssembly_WhenInspectingReferences_ThenItDoesNotReferenceInfrastructureOrApi`

## 4. Api（`CoNotes.Api`）

> 開發環境備註：`Authentication:Authority`/`OpenTelemetry:OtlpEndpoint` 指向的 Keycloak／SigNoz 在這個沙盒裡都不存在。4.2 的 JWT Bearer pipeline 改用 `FunctionalTestWebAppFactory` 覆寫 `TokenValidationParameters`（對稱金鑰簽章）來驗證真正的驗證/拒絕邏輯，沒有打過真的 Keycloak；4.9 用 `ActivityListener` 驗證 ASP.NET Core instrumentation 真的有產生 span，但沒有接到真的 SigNoz 去看 UI。

- [x] 4.1 建立 .NET 10 API 專案骨架，加入不需驗證的健康檢查 endpoint
- [x] 4.2 設定 `AddAuthentication().AddJwtBearer()`，`Authority` 指向 Keycloak Realm 的 issuer URL，`Audience` 指向 client id
- [x] 4.3 建立一個受 `[Authorize]` 保護的測試 endpoint，登入成功後分派 `UpsertAppUserCommand`
- [x] 4.4 功能測試（真實 HTTP pipeline）：`GivenValidToken_WhenCallingProtectedEndpoint_ThenReturns200AndUpsertsAppUser`
- [x] 4.5 功能測試：`GivenNoToken_WhenCallingProtectedEndpoint_ThenReturns401`
- [x] 4.6 功能測試：`GivenExpiredOrInvalidSignatureToken_WhenCallingProtectedEndpoint_ThenReturns401`
- [x] 4.7 功能測試：`GivenSameUserCallsTwice_WhenSecondRequestArrives_ThenNoDuplicateAppUserIsCreated`
- [x] 4.8 功能測試：`GivenNoToken_WhenCallingHealthCheckEndpoint_ThenReturns200`
- [ ] 4.9 埋 OpenTelemetry 儀器化，OTLP exporter 指向 SigNoz，驗證呼叫 API 後可在 SigNoz UI 看到對應 trace（instrumentation 已驗證會產生 span，SigNoz UI 部分無法驗證）
- [ ] 4.10 撰寫 API 的 k8s manifest，部署後確認 pod Running

## 5. 端對端驗證

- [ ] 5.1 確認 ArgoCD Application 同步 `deploy/k8s` 成功，所有 workload 狀態為 Synced/Healthy
- [ ] 5.2 從外部瀏覽器實測：`auth.<domain>` 可看到 Keycloak 登入頁；完成登入後，用取得的 token 呼叫 `api.<domain>` 的受保護 endpoint 可取得正常回應
- [ ] 5.3 逐一驗證 `specs/identity/authentication/spec.md` 的四個 Requirement 全數通過
- [ ] 5.4 把部署順序與已知限制（單副本 Keycloak、共用 Postgres instance）記錄到 repo 說明文件，供之後重建環境參考
