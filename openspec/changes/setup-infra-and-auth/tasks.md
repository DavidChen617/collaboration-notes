## 1. Domain（`CoNotes.Domain`）

- [x] 1.1 定義 `AppUser` Aggregate Root（`Id`、`KeycloakSub`、`CreatedAt`），提供建立新 `AppUser` 的工廠方法，建立時記錄 `AppUserProvisioned` Domain Event
- [x] 1.2 單元測試（NSubstitute 不需要，純 Domain 邏輯）：`GivenNewKeycloakSub_WhenCreatingAppUser_ThenRaisesAppUserProvisionedEvent`
- [x] 1.3 定義 `IAppUserRepository` 介面（`FindByKeycloakSubAsync`、`AddAsync`），供 Application 層依賴、Infrastructure 層實作

## 2. Application（`CoNotes.Application`）

- [x] 2.1 實作 `UpsertAppUserCommand` + `UpsertAppUserCommandHandler`：查詢 `IAppUserRepository` 是否已有對應 `AppUser`，沒有就建立新的並寫入，已有就直接回傳既有記錄
- [x] 2.2 單元測試（mock `IAppUserRepository`）：`GivenAppUserDoesNotExist_WhenHandlingUpsertAppUserCommand_ThenCreatesAndPersistsNewAppUser`
- [x] 2.3 單元測試（mock `IAppUserRepository`）：`GivenAppUserAlreadyExists_WhenHandlingUpsertAppUserCommand_ThenReturnsExistingAppUserWithoutDuplication`

## 3. Infrastructure（`CoNotes.Infrastructure` + 叢集基礎設施）

> 開發環境備註：這裡沒有 kubectl/k8s cluster，3.3/3.6/3.9/3.10 之後只寫 manifest，不勾選「部署後確認」的部分。manifests 現在放在巢狀的 `infra/k8s/<workload>/` 資料夾下，因此把 `infra/argocd/application.yaml` 的 `source.directory.recurse` 打開，ArgoCD 才能找到子資料夾內的檔案。ingress-nginx／SigNoz 改用官方 Helm chart（透過 child ArgoCD Application，app-of-apps 模式），沒有手刻它們的 raw manifest——upstream 自己的 manifest 很大（RBAC、webhook 設定等）、SigNoz 實際拓樸也複雜（ClickHouse、otel-collector、query-service…），手刻風險太高。
>
> **後續補上**：先前這裡記錄「沒有可用的 Docker daemon」而卡住 3.2——後來確認這個環境其實有 Docker（`docker info` 正常），跑 `dotnet test tests/CoNotes.IntegrationTests` 後 `AppUserRepositoryTests` 通過，3.2 補打勾。過程中也發現 `FunctionalTestWebAppFactory` 沒有像 `IntegrationTestWebAppFactory` 一樣起自己的 Testcontainers Postgres、而是寫死指向 `localhost`，導致在乾淨環境（含之後 CI）連不到資料庫、所有 Functional Test 500——已修正（見 `tests/CoNotes.FunctionalTests/FunctionalTestWebAppFactory.cs`），16 個 Functional Test 全數改為通過。

> **後續補上（notes-crud 期間補的）**：`AppUserRepository` 原本直接用 `IDbConnectionFactory`，後來在 notes-crud 補 `AppDbContext`/`UnitOfWork`/`TransactionalDecorator` 時一併補上，改成注入 `AppDbContext`、`AddAsync` 呼叫 `TrackAggregateRoot`，`UpsertAppUserCommand` 也掛上 `TransactionalDecorator`。細節見 `openspec/changes/notes-crud/tasks.md` 第 3 節的說明。

- [x] 3.1 實作 `IAppUserRepository` 的 Dapper 版本（`AppUserRepository`），透過 `IDbConnectionFactory` 取得連線
- [x] 3.2 整合測試（Testcontainers 起真的 Postgres）：`GivenPostgresDatabase_WhenUpsertingSameKeycloakSubTwice_ThenSecondCallReturnsSameAppUserRow`（已在有 Docker 的環境跑 `dotnet test tests/CoNotes.IntegrationTests` 驗證通過）
- [ ] 3.3 撰寫 Postgres 的 k8s manifest（Deployment/StatefulSet + Service + PVC），部署後用 `kubectl get pods` 確認 pod 為 Running 狀態（manifest 已寫於 `infra/k8s/postgres/`，未部署確認）
- [x] 3.4 建立 `app`、`keycloak` 兩個 database，並確認可分別用對應帳號連線成功
- [x] 3.5 安裝 `golang-migrate` CLI，撰寫第一版 migration 建立 `AppUser` table，執行後用 `migrate version` 確認已套用到最新版本；驗證 down/up 都能重複執行不報錯
- [ ] 3.6 撰寫 Keycloak 的 k8s manifest（Deployment + Service），指向 `keycloak` database，部署後確認 pod Running 且能連上該 database（manifest 已寫於 `infra/k8s/keycloak/`，未部署確認）
- [x] 3.7 手動建立 Realm 與一個開啟 PKCE 的 public client，設定合法的 redirect URI；匯出成 realm-export JSON 存進 repo，並驗證用此檔案可重新匯入出一致的 Realm（沒有 k8s，但 Keycloak 本身是 Java 應用、不一定要容器化：直接在本機裝 JDK 21 + Keycloak 26.7.3 distribution，接本機的 `keycloak` database 跑 `--import-realm`，用 admin REST API 讀回 Realm/Client 設定，逐欄位比對跟 `conotes-realm.json` 完全一致。過程中發現兩個真的問題並已修正：① `redirectUris`/`webOrigins` 原本寫 `https://app.<domain>/*`，`<`/`>` 不是合法 URI 字元，Keycloak 直接拒絕匯入，已改用 `example.com` 佔位；② public client 預設核發的 access token `aud` 只有 `account`，不會是 `conotes-spa`，跟 design.md 決策 5「Audience 設成 SPA 的 client id」的假設對不上，已在 client 定義加上 `oidc-audience-mapper`（`protocolMappers`），重新從乾淨 DB 匯入驗證過 `aud` 正確變成 `["conotes-spa","account"]`）
- [x] 3.8 手動走一次 Keycloak 登入頁面完成登入，確認可取得 access token（沒有瀏覽器，改用 curl 模擬瀏覽器實際會發的 HTTP 請求：走 Authorization Code + PKCE 全流程——GET `/auth`、解析登入表單、POST 帳密、從 302 redirect 撈 `code`、用 `code_verifier` 換 token——truly 拿到一組有效 access token。接著更進一步：把這組真的 token 拿去打本機啟動的 `CoNotes.Api`（`Authority` 指向這個真的 Keycloak），完整走一次 401→200、UpsertAppUserCommand、Postgres 寫入、重複呼叫不重複建立，全部通過，比 task 原本要求的還完整。過程中額外發現 `RequireHttpsMetadata` 預設 `true` 會擋掉本機這種跑在 http 的 Keycloak，已在 `Program.cs` 加上 `!IsDevelopment()` 判斷修正，functional tests 全數重跑仍然通過）
- [ ] 3.9 撰寫 SigNoz 的 k8s manifest，部署後確認可以開啟 SigNoz 的 UI（改用官方 Helm chart，見 `infra/k8s/signoz/application.yaml` 這個 child ArgoCD Application；SigNoz 實際拓樸複雜，手刻 raw manifest 風險太高，未部署確認）
- [ ] 3.10 部署 ingress-nginx controller，確認其 Service 已取得可用的內部位址；建立 Ingress 規則，把 `api.<domain>` 導向 API Service、`auth.<domain>` 導向 Keycloak Service（controller 同樣改用官方 Helm chart，見 `infra/k8s/ingress-nginx/application.yaml`；Ingress 規則見 `infra/k8s/ingress/ingress.yaml`；未部署確認）
- [ ] 3.11 更新 Cloudflare Tunnel 設定，將目標指向 nginx 的 Service（設定檔已寫於 `infra/cloudflared/config.yml`；實際套用到線上 Tunnel 是這個 repo／sandbox 以外的操作，無法在此驗證）
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
- [ ] 4.9 埋 OpenTelemetry 儀器化，OTLP exporter 指向 SigNoz，驗證呼叫 API 後可在 SigNoz UI 看到對應 trace（instrumentation 已驗證會產生 span。把 sandbox 的 Docker VM 記憶體從 ~2GB 提高到 6GB 後解決了 OOM，接著發現一個真的程式碼 bug：OTLP exporter 預設用 gRPC，對明碼（非 TLS）的 collector 端點會 `HTTP/2 handshake` 失敗——這不是本機限定的問題，我們的內部流量本來就是明碼，k8s 上也會遇到，已改成 `OtlpExportProtocol.HttpProtobuf`（見 `Program.cs`、`appsettings.json`、`infra/k8s/api/configmap.yaml` 改成 `:4318`）。改完之後仍未完整驗證到「trace 出現在 SigNoz」：這次用 `foundryctl v0.2.17` 產生的本機 SigNoz 安裝，`ingester` 持續跟自己的 OpAmp 設定伺服器握手失敗，實測送出的 export 請求拿到「連線建立但沒回應」，ClickHouse 那邊持續 0 筆——判斷是這次本機 SigNoz 安裝本身的問題，不是本專案的問題，但還沒找到根因，留待之後在別的環境或別的 `foundryctl` 版本重試，細節見 `infra/README.md`）
- [ ] 4.10 撰寫 API 的 k8s manifest，部署後確認 pod Running（manifest 已寫於 `infra/k8s/api/`，未部署確認）

## 5. 端對端驗證

> 5.1/5.2 需要真的 k8s cluster 與對外網域，這個 sandbox 沒有，無法驗證。

- [ ] 5.1 確認 ArgoCD Application 同步 `infra/k8s` 成功，所有 workload 狀態為 Synced/Healthy（阻塞：無 k8s cluster）
- [ ] 5.2 從外部瀏覽器實測：`auth.<domain>` 可看到 Keycloak 登入頁；完成登入後，用取得的 token 呼叫 `api.<domain>` 的受保護 endpoint 可取得正常回應（真的外部網域／瀏覽器阻塞：無 k8s cluster／對外網域。但底層的登入→取得 token→呼叫受保護 endpoint 這條路，已經在本機用真的 Keycloak + 真的 API 完整跑過一次，見 3.8 的說明，全部正常回應；缺的只是「外部瀏覽器」跟「真的網域」這兩個環境條件）
- [x] 5.3 逐一驗證 `specs/identity/authentication/spec.md` 的四個 Requirement 全數通過（四個 Requirement 分別對應 `AppUserEndpointTests`/`HealthEndpointTests` 裡的 6 個 functional test，全數通過；唯一沒驗證到的是真的瀏覽器 OIDC 登入導轉，見 5.2）
- [x] 5.4 把部署順序與已知限制（單副本 Keycloak、共用 Postgres instance）記錄到 repo 說明文件，供之後重建環境參考（`infra/README.md`）
