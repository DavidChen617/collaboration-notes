## 1. Domain（`CoNotes.Domain`）

- [x] 1.1 定義 `AppUser` Aggregate Root（`Id`、`KeycloakSub`、`CreatedAt`），提供建立新 `AppUser` 的工廠方法，建立時記錄 `AppUserProvisioned` Domain Event
- [x] 1.2 單元測試（NSubstitute 不需要，純 Domain 邏輯）：`GivenNewKeycloakSub_WhenCreatingAppUser_ThenRaisesAppUserProvisionedEvent`
- [x] 1.3 定義 `IAppUserRepository` 介面（`FindByKeycloakSubAsync`、`AddAsync`），供 Application 層依賴、Infrastructure 層實作

## 2. Application（`CoNotes.Application`）

- [x] 2.1 實作 `UpsertAppUserCommand` + `UpsertAppUserCommandHandler`：查詢 `IAppUserRepository` 是否已有對應 `AppUser`，沒有就建立新的並寫入，已有就直接回傳既有記錄
- [x] 2.2 單元測試（mock `IAppUserRepository`）：`GivenAppUserDoesNotExist_WhenHandlingUpsertAppUserCommand_ThenCreatesAndPersistsNewAppUser`
- [x] 2.3 單元測試（mock `IAppUserRepository`）：`GivenAppUserAlreadyExists_WhenHandlingUpsertAppUserCommand_ThenReturnsExistingAppUserWithoutDuplication`

## 3. Infrastructure（`CoNotes.Infrastructure` + 叢集基礎設施）

> 開發環境備註（更新）：一開始這裡沒有 kubectl/k8s cluster，3.3/3.6/3.9/3.10 之後只寫 manifest，不勾選「部署後確認」的部分。後來你架好了一個真的 3 節點 kubeadm 叢集（2 台 Linux + 1 台跑 Linux VM 的 Mac mini，彼此用 Tailscale 溝通），ArgoCD 也已經裝好——這幾項因此都補上真的部署驗證。過程中額外抓到兩個真的 bug：① ArgoCD 的 `Application` 資源(`infra/argocd/application.yaml`) 一直指向錯誤的 repo(`collaboration-notes`，但實際 repo 當時叫 `cobllaboration-notes`，多一個 `b`——已把 GitHub repo 改名成 `collaboration-notes` 修正) 跟錯誤的路徑(`deploy/k8s`，但 manifest 其實都在 `infra/k8s/`)，導致 ArgoCD 完全無法同步；② repo 裡本來就有 61 個從沒推上 GitHub 的本機 commit，已確認過沒有敏感資訊後推上去。manifests 放在巢狀的 `infra/k8s/<workload>/` 資料夾下，`infra/argocd/application.yaml` 的 `source.directory.recurse` 打開讓 ArgoCD 找到子資料夾內的檔案。ingress-nginx／SigNoz 改用官方 Helm chart（透過 child ArgoCD Application，app-of-apps 模式），沒有手刻它們的 raw manifest——upstream 自己的 manifest 很大（RBAC、webhook 設定等）、SigNoz 實際拓樸也複雜（ClickHouse、otel-collector、query-service…），手刻風險太高。
>
> **後續補上**：先前這裡記錄「沒有可用的 Docker daemon」而卡住 3.2——後來確認這個環境其實有 Docker（`docker info` 正常），跑 `dotnet test tests/CoNotes.IntegrationTests` 後 `AppUserRepositoryTests` 通過，3.2 補打勾。過程中也發現 `FunctionalTestWebAppFactory` 沒有像 `IntegrationTestWebAppFactory` 一樣起自己的 Testcontainers Postgres、而是寫死指向 `localhost`，導致在乾淨環境（含之後 CI）連不到資料庫、所有 Functional Test 500——已修正（見 `tests/CoNotes.FunctionalTests/FunctionalTestWebAppFactory.cs`），16 個 Functional Test 全數改為通過。

> **後續補上（notes-crud 期間補的）**：`AppUserRepository` 原本直接用 `IDbConnectionFactory`，後來在 notes-crud 補 `AppDbContext`/`UnitOfWork`/`TransactionalDecorator` 時一併補上，改成注入 `AppDbContext`、`AddAsync` 呼叫 `TrackAggregateRoot`，`UpsertAppUserCommand` 也掛上 `TransactionalDecorator`。細節見 `openspec/changes/notes-crud/tasks.md` 第 3 節的說明。

- [x] 3.1 實作 `IAppUserRepository` 的 Dapper 版本（`AppUserRepository`），透過 `IDbConnectionFactory` 取得連線
- [x] 3.2 整合測試（Testcontainers 起真的 Postgres）：`GivenPostgresDatabase_WhenUpsertingSameKeycloakSubTwice_ThenSecondCallReturnsSameAppUserRow`（已在有 Docker 的環境跑 `dotnet test tests/CoNotes.IntegrationTests` 驗證通過）
- [x] 3.3 撰寫 Postgres 的 k8s manifest（Deployment/StatefulSet + Service + PVC），部署後用 `kubectl get pods` 確認 pod 為 Running 狀態（真的部署到你的 3 節點 kubeadm 叢集上驗證過：`kubectl get pods -n collaboration-notes` 顯示 `postgres` 為 `1/1 Running`；過程中發現叢集完全沒有任何 StorageClass，PVC 永遠 `Pending`（`0/3 nodes are available: pod has unbound immediate PersistentVolumeClaims`）——已裝上官方 Rancher `local-path-provisioner`(v0.0.31，見新增的 `infra/k8s/local-path-provisioner/`，跟 k3s 內建那套同源) 並設成預設 StorageClass，PVC 才綁定成功、pod 才排得上去）
- [x] 3.4 建立 `app`、`keycloak` 兩個 database，並確認可分別用對應帳號連線成功
- [x] 3.5 安裝 `golang-migrate` CLI，撰寫第一版 migration 建立 `AppUser` table，執行後用 `migrate version` 確認已套用到最新版本；驗證 down/up 都能重複執行不報錯
- [x] 3.6 撰寫 Keycloak 的 k8s manifest（Deployment + Service），指向 `keycloak` database，部署後確認 pod Running 且能連上該 database（真的部署驗證過：`kubectl exec` 進 pod 讀 `KC_DB_URL` 確認是 `jdbc:postgresql://postgres:5432/keycloak`，`kubectl logs` 看到 `database system is ready to accept connections` 之後 Keycloak 成功啟動、pod 為 `1/1 Running`；ConfigMap `keycloak-realm-import.yaml` 是手動維護的 `conotes-realm.json` 副本，過程中發現它已經跟真正的來源檔案(`realm-export/conotes-realm.json`)不同步——後者已經在 subscription-billing change 加了 `admin` realm role，前者沒有跟著更新，已補上；另外把來源 JSON 檔案排除在 ArgoCD 的目錄掃描之外(`directory.exclude`)，因為它沒有 `kind` 欄位、被 ArgoCD 當成 manifest 嘗試解析會直接讓整個 Application 的 comparison 失敗)
- [x] 3.7 手動建立 Realm 與一個開啟 PKCE 的 public client，設定合法的 redirect URI；匯出成 realm-export JSON 存進 repo，並驗證用此檔案可重新匯入出一致的 Realm（沒有 k8s，但 Keycloak 本身是 Java 應用、不一定要容器化：直接在本機裝 JDK 21 + Keycloak 26.7.3 distribution，接本機的 `keycloak` database 跑 `--import-realm`，用 admin REST API 讀回 Realm/Client 設定，逐欄位比對跟 `conotes-realm.json` 完全一致。過程中發現兩個真的問題並已修正：① `redirectUris`/`webOrigins` 原本寫 `https://app.<domain>/*`，`<`/`>` 不是合法 URI 字元，Keycloak 直接拒絕匯入，已改用 `example.com` 佔位；② public client 預設核發的 access token `aud` 只有 `account`，不會是 `conotes-spa`，跟 design.md 決策 5「Audience 設成 SPA 的 client id」的假設對不上，已在 client 定義加上 `oidc-audience-mapper`（`protocolMappers`），重新從乾淨 DB 匯入驗證過 `aud` 正確變成 `["conotes-spa","account"]`）
- [x] 3.8 手動走一次 Keycloak 登入頁面完成登入，確認可取得 access token（沒有瀏覽器，改用 curl 模擬瀏覽器實際會發的 HTTP 請求：走 Authorization Code + PKCE 全流程——GET `/auth`、解析登入表單、POST 帳密、從 302 redirect 撈 `code`、用 `code_verifier` 換 token——truly 拿到一組有效 access token。接著更進一步：把這組真的 token 拿去打本機啟動的 `CoNotes.Api`（`Authority` 指向這個真的 Keycloak），完整走一次 401→200、UpsertAppUserCommand、Postgres 寫入、重複呼叫不重複建立，全部通過，比 task 原本要求的還完整。過程中額外發現 `RequireHttpsMetadata` 預設 `true` 會擋掉本機這種跑在 http 的 Keycloak，已在 `Program.cs` 加上 `!IsDevelopment()` 判斷修正，functional tests 全數重跑仍然通過）
- [x] 3.9 撰寫 SigNoz 的 k8s manifest，部署後確認可以開啟 SigNoz 的 UI（改用官方 Helm chart，見 `infra/k8s/signoz/application.yaml` 這個 child ArgoCD Application；SigNoz 實際拓樸複雜，手刻 raw manifest 風險太高；真的部署到叢集上驗證過：ArgoCD 顯示這個 child Application 是 `Synced`/`Healthy`，`kubectl port-forward` 連進 `signoz` Service 後 `curl` 回 200，UI 真的能開)
- [x] 3.10 部署 ingress-nginx controller，確認其 Service 已取得可用的內部位址；建立 Ingress 規則，把 `api.<domain>` 導向 API Service、`auth.<domain>` 導向 Keycloak Service（controller 同樣改用官方 Helm chart，見 `infra/k8s/ingress-nginx/application.yaml`；Ingress 規則見 `infra/k8s/ingress/ingress.yaml`；真的部署驗證過：`ingress-nginx-controller` pod `1/1 Running`，Service 取得可用的 ClusterIP(`10.107.116.217`)——`EXTERNAL-IP` 欄位維持 `<pending>`，這是因為裸機 kubeadm 叢集沒有 MetalLB 之類的 LoadBalancer 實作，不影響「取得可用的內部位址」這個任務標準；`kubectl get ingress` 確認 `conotes` 這條 Ingress 規則真的套用成功，`hosts` 正確顯示 `api.example.com`／`auth.example.com`)
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
- [ ] 4.9 埋 OpenTelemetry 儀器化，OTLP exporter 指向 SigNoz，驗證呼叫 API 後可在 SigNoz UI 看到對應 trace（instrumentation 已驗證會產生 span，`OtlpExportProtocol.HttpProtobuf` 的修正見下方說明。之前卡在本機 `foundryctl` 產生的 SigNoz 安裝本身故障——現在叢集上用官方 Helm chart 部署的 SigNoz 已經確認 `Healthy`、UI 也連得上（見 3.9），這個舊阻塞已經解除；但新的阻塞是 4.10：`api` 這個 Deployment 目前找不到 image(`ghcr.io/davidchen617/conotes-api:latest` 不存在)，pod 起不來，沒有真正在跑的 API 可以送 trace，要等 `cicd-deployment` change 把 image 建好推上 ghcr.io 才能真的驗證這一項)
- [ ] 4.10 撰寫 API 的 k8s manifest，部署後確認 pod Running（manifest 已寫於 `infra/k8s/api/`；真的部署到叢集上，Deployment／Service 都成功建立，但 pod 卡在 `ImagePullBackOff`——`ghcr.io/davidchen617/conotes-api:latest` 這個 image 從來沒有被建置/推送過，這正是 `cicd-deployment` change 要做的事，目前完全還沒開始。manifest 本身結構正確，只是缺一個真的可以拉下來的 image）

## 5. 端對端驗證

> 5.1/5.2 原本需要真的 k8s cluster 與對外網域，這個 sandbox 沒有，無法驗證；現在有真的叢集了，5.1 大部分驗證過，5.2 仍卡在「真的外部網域」。

- [ ] 5.1 確認 ArgoCD Application 同步 `infra/k8s` 成功，所有 workload 狀態為 Synced/Healthy（真的對你的叢集驗證過：`collaboration-notes`／`ingress-nginx`／`signoz` 三個 Application 都是 `Synced`；`signoz` 已經 `Healthy`，`ingress-nginx` 因為裸機叢集沒有 LoadBalancer 實作、Service 的 `EXTERNAL-IP` 停在 `pending`，ArgoCD 因此判定成 `Progressing`(非功能性問題，見 3.10 說明)，`collaboration-notes` 則因為 `api` pod 卡在 `ImagePullBackOff`(見 4.10) 也是 `Progressing`——要等 `cicd-deployment` 把 image 建好、`ingress-nginx` 的健康判斷放寬或裝 MetalLB，才能全部變成 `Healthy`）
- [ ] 5.2 從外部瀏覽器實測：`auth.<domain>` 可看到 Keycloak 登入頁；完成登入後，用取得的 token 呼叫 `api.<domain>` 的受保護 endpoint 可取得正常回應（「瀏覽器」這半句後來補上了——用 Playwright 裝的真實 headless Chromium，跑過一次 Angular SPA → Keycloak 登入頁 → 導回 SPA → 呼叫受保護 API 的完整瀏覽器流程，見 `notes-crud` tasks.md 第 5 節。仍然阻塞的只剩「真的外部網域」：這裡走的是 `localhost`，不是 `auth.<domain>`/`api.<domain>`，需要真的 k8s cluster + Cloudflare Tunnel 才能驗證）
- [x] 5.3 逐一驗證 `specs/identity/authentication/spec.md` 的四個 Requirement 全數通過（四個 Requirement 分別對應 `AppUserEndpointTests`/`HealthEndpointTests` 裡的 6 個 functional test，全數通過。**後續補充**：這些測試當時全部透過 `/api/test/app-user` 明確觸發 upsert，掩蓋了一個真的 bug——`AppUser` 的建立其實只掛在這一支測試 endpoint 上，換成任何其他受保護 endpoint（例如 Notes）第一次呼叫都會 500。已修正為在 JWT Bearer 的 `OnTokenValidated` 事件觸發，任何受保護 endpoint 都適用，細節見 `notes-crud` tasks.md 第 5 節與 commit `45b1bbb`。真的瀏覽器 OIDC 導轉，見 5.2）
- [x] 5.4 把部署順序與已知限制（單副本 Keycloak、共用 Postgres instance）記錄到 repo 說明文件，供之後重建環境參考（`infra/README.md`）
