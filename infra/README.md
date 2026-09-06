# 部署說明

記錄 `setup-infra-and-auth` 這個 change 的部署順序與已知限制，供之後重建環境參考。完整的決策理由見
[`openspec/changes/setup-infra-and-auth/design.md`](../openspec/changes/setup-infra-and-auth/design.md)。

## 待辦事項（改用 davish.net 這個真實網域時中斷，還沒完成）

- [x] ~~前端網域待確認~~：確定用 `https://www.davish.net/conote`（自訂網域下的子路徑，不是 GitHub Pages
      預設網址）。已完成：`infra/k8s/keycloak/realm-export/conotes-realm.json` 跟鏡像
      `infra/k8s/keycloak/configmap-realm-import.yaml` 的 SPA client `redirectUris` 改成
      `https://www.davish.net/conote/*`、`webOrigins` 改成 `https://www.davish.net`（Web Origin 只看
      protocol+host+port，不含路徑，所以不用帶 `/conote`）；`.github/workflows/deploy.yml` 的
      `--base-href` 改成 `/conote/`，並新增一步把建置輸出包進 `conote/` 子目錄再上傳（GitHub Pages
      完全照 artifact 的目錄結構原樣提供服務，這是唯一能讓自訂網域服務子路徑的方式）——已在本機用
      `pnpm build --base-href /conote/` 實際跑過一次，確認輸出結構跟 `index.html` 的
      `<base href="/conote/">` 都正確。repo 的 GitHub Pages 設定已透過 `gh api` 建立並把 `cname` 設成
      `www.davish.net`（`https_enforced` 目前是 `false`，等 DNS 生效、GitHub 核發憑證後會自動變
      `true`）。
- [ ] **DNS 記錄**：Cloudflare 的 `davish.net` zone 裡要新增一筆 `CNAME`，`www` → `davidchen617.github.io`
      （這是一般 DNS 記錄，**不是** Cloudflare Tunnel 的 Public Hostname——GitHub Pages 是 GitHub 自己
      的公開服務，不經過你的 tunnel/叢集）。
- [ ] **Cloudflare Tunnel 的 Public Hostname 規則**：你已經建立 tunnel，還需要新增
      `api.davish.net` → `ingress-nginx-controller.ingress-nginx.svc.cluster.local:80` 這條規則。
      **`auth.davish.net` 你這次沒提到，但 Keycloak 的登入是瀏覽器直接導向的流程（design.md 決定 9），
      沒有這條規則登入會整個失敗——要不要一起加上？**
- [ ] 拿到 tunnel 的 token 後填進 `.env` 的 `TUNNEL_TOKEN=`，跑一次 `infra/scripts/apply-secrets.sh`。
      詳細步驟見下方「Cloudflare Tunnel」章節。
- [ ] `openspec/changes/cicd-deployment` task 3.3：repo 的 GitHub Actions workflow 權限要手動切成
      「Read and write permissions」（`Settings → Actions → General → Workflow permissions`），這個 API
      呼叫被 Claude Code 的權限分類器擋下，需要手動處理。
- [ ] 這一輪關於 `davish.net`／Cloudflare Tunnel／`apply-secrets.sh` 改回單一檔案的改動目前都還**沒
      commit**（本地 working tree），也還沒 push。

## 部署順序

1. **Postgres**（`infra/k8s/postgres/`）：單一 instance，透過 init script（`configmap-init.yaml`）在第一次啟動時建立 `app`、`keycloak` 兩個 database 與對應帳號。
2. **Migration**：用 `golang-migrate` 對 `app` database 套用 `src/CoNotes.Infrastructure/Persistence/Migrations/` 底下的 SQL，建立 `app_users` table。
3. **Keycloak**（`infra/k8s/keycloak/`）：指向 `keycloak` database；`--import-realm` 會在啟動時自動匯入 `realm-export/conotes-realm.json` 定義的 Realm 與 public client。
4. **SigNoz**（`infra/k8s/signoz/application.yaml`）：以官方 Helm chart 部署（child ArgoCD Application）。
5. **ingress-nginx controller**（`infra/k8s/ingress-nginx/application.yaml`）：以官方 Helm chart 部署（child ArgoCD Application）。
6. **API**（`infra/k8s/api/`）：JWT Bearer 的 `Authority` 指向 Keycloak Realm、OTLP exporter 指向 SigNoz 的 otel-collector。
7. **Ingress 規則**（`infra/k8s/ingress/ingress.yaml`）：`api.davish.net` → API Service、`auth.davish.net` → Keycloak Service。
8. **Cloudflare Tunnel**（`infra/k8s/cloudflared/`）：跑在叢集裡的 Deployment，用 token 模式連回 Cloudflare——tunnel 本身跟 public hostname 路由規則都在 Cloudflare Zero Trust 後台設定，不是本地檔案。建立步驟見下方「Cloudflare Tunnel」章節。
9. **ArgoCD**（`infra/argocd/application.yaml`）：指向 `infra/k8s`，`directory.recurse: true` 讓它能找到巢狀資料夾裡的 manifest。

## 密鑰管理（PayPal／AI provider／DB／Keycloak admin／Cloudflare Tunnel）

`cicd-deployment` change 的決定：這類密鑰完全不進 CI/CD、也不進 git（見
`openspec/changes/cicd-deployment/design.md` 決定 5）。維護方式是伺服器端一份
`.env` 檔案，手動執行 `infra/scripts/apply-secrets.sh` 套用成對應的 k8s Secret；
`infra/k8s` 底下有對應這幾個 Secret 的「空殼」manifest（只宣告存在與 key 名稱，
`stringData` 是空字串），讓 ArgoCD 能在新叢集 bootstrap 時建出這些物件，但
`infra/argocd/application.yaml` 對它們設定了 `ignoreDifferences`（忽略
`data`/`stringData`），所以 ArgoCD 不會把伺服器端套用的真實內容用 `selfHeal`
蓋回空殼，也不會因為内容跟 git 不同就當成 drift。

- **`.env` 放在哪裡**：叢集任一台能操作 `kubectl` 的節點上，單一檔案，路徑固定用
  `/etc/conotes/secrets.env`（`apply-secrets.sh` 的預設路徑，也可以在執行時
  另外指定路徑當第一個參數）。這份檔案不進 git，只存在伺服器端，腳本會依照
  key 名稱自動分流到對應的 Secret：

  ```
  # -> Secret conotes-secrets（API 用 envFrom 整包注入，key 對應
  #    CoNotes.Api 的 configuration key，把 : 換成 __；Ai__* 允許留空，
  #    程式碼會把對應的 provider 當作「不可用」跳過，不會讓 API 啟動失敗）
  PayPal__ClientId=...
  PayPal__ClientSecret=...
  PayPal__WebhookId=...
  Ai__Groq__ApiKey=...
  Ai__Gemini__ApiKey=...

  # -> Secret postgres-credentials（Postgres/Keycloak 的 Deployment 用
  #    secretKeyRef 個別讀取單一 key）
  postgres-password=...
  app-password=...
  keycloak-password=...

  # -> Secret keycloak-credentials（Keycloak admin 帳號登入用，注意跟
  #    上面的 keycloak-password 是兩件不同的事：那個是 Keycloak 拿去連
  #    資料庫用的密碼，這個是 Keycloak 管理後台網頁登入密碼）
  admin-password=...

  # -> Secret cloudflared-credentials（見下方「Cloudflare Tunnel」章節）
  TUNNEL_TOKEN=...
  ```

- **什麼時候要重新執行**：第一次建立叢集時、或任何一組密鑰輪替/更新時。指令：
  ```bash
  ./infra/scripts/apply-secrets.sh /etc/conotes/secrets.env
  ```
  一次會把 `.env` 裡的 key 分別套用成上面四個 Secret（某個 Secret 對應的 key
  都不存在時會跳過該 Secret，不會報錯中斷）。執行後 Secret 內容更新，但
  **不會**自動讓對應的 pod 重新啟動去讀新的環境變數（k8s 的 Secret 更新不會
  觸發已存在 pod 的 env 重新載入）——需要額外手動重啟，例如：
  ```bash
  kubectl rollout restart deployment/api -n collaboration-notes
  kubectl rollout restart deployment/postgres -n collaboration-notes
  kubectl rollout restart deployment/keycloak -n collaboration-notes
  kubectl rollout restart deployment/cloudflared -n collaboration-notes
  ```

## Cloudflare Tunnel

`cloudflared` 跑在叢集裡（`infra/k8s/cloudflared/deployment.yaml`），用 token
模式運作：tunnel 本身、以及 `api.davish.net`/`auth.davish.net` 這兩條 public
hostname 規則，都在 Cloudflare Zero Trust 後台設定，不是本地 config 檔案——
這個 Deployment 只需要一個 `TUNNEL_TOKEN` 就能運作。

**建立步驟**（在 Cloudflare Zero Trust 後台操作，這一步在 repo 之外）：
1. `Networks → Tunnels → Create a tunnel`，connector 類型選 `Cloudflared`，取個名字（例如 `conotes`）。
2. 建立後會看到一段帶 token 的安裝指令，只需要複製 `--token` 後面那串值。
3. 在 `Public Hostname` 分頁新增兩條規則：
   - `api.davish.net` → HTTP → `ingress-nginx-controller.ingress-nginx.svc.cluster.local:80`
   - `auth.davish.net` → HTTP → `ingress-nginx-controller.ingress-nginx.svc.cluster.local:80`
4. 把拿到的 token 填進 `.env` 檔案的 `TUNNEL_TOKEN=`，跑一次 `infra/scripts/apply-secrets.sh`（見上方「密鑰管理」）。

DNS 記錄（`api.davish.net`/`auth.davish.net` 的 CNAME 指到 tunnel）由 Cloudflare
在你新增 Public Hostname 規則時自動建立，前提是 `davish.net` 這個 zone 本身已經
在用 Cloudflare 的 nameserver。

## 已知限制

- **單副本 Keycloak**：沒有用官方 Operator，也沒有多副本；它是單點故障，掛掉之後沒人能登入（既有的 access token 在過期前仍可正常呼叫 API）。
- **Keycloak 用 `start-dev`**：為求簡化，不是生產模式（`start`）。可接受，因為這個 change 明確排除生產級硬化／HA。
- **共用同一個 Postgres instance**：`app`、`keycloak` 是同一個 instance 上的兩個獨立 database，不是獨立 instance；這個 instance 掛掉會同時拖垮登入跟 App 資料。用獨立 database（不是 schema）保留了之後切到獨立 instance 的路徑。
- **DB／Keycloak admin 密碼佔位**：這是玩具/學習專案，`.env` 目前仍填的是佔位密碼；正式使用前必須替換成真的密碼，流程一樣是改 `.env` 後重跑 `infra/scripts/apply-secrets.sh`（見上方「密鑰管理」）。
- **沒有設定任何 pod resource requests/limits**：design.md 的 Open Question，留到實際觀察用量後再調整。
- **`Redis` 的 k8s manifest 尚未撰寫**：`proposal.md` 提到要跟其他資料層一起建起來（給 SignalR backplane 用），但 `tasks.md` 當時沒有對應的 task item，一直沒有 Redis 的 k8s manifest。`collab-editing` change 實作 SignalR Hub 時才發現這個落差，先在本機 `docker-compose` 補上 Redis 容器（見下方「本機開發」），k8s manifest 仍待補。

## 本機開發（docker-compose）

不需要 k8s cluster，`docker-compose.yaml`（repo 根目錄）啟動 Postgres + migration + Keycloak 這組資料層/驗證層，API 本身用 `dotnet run` 在本機跑，指向這組容器：

```bash
docker compose up -d
```

- `postgres`：對應 `infra/k8s/postgres/` 的邏輯，`infra/postgres-init/` 底下的 init script 建立 `app`、`keycloak` 兩個 database（密碼都是明碼 `password`，僅供本機開發用）。
- `migrate`：用官方 `migrate/migrate` image 對 `app` database 套用 `src/CoNotes.Infrastructure/Persistence/Migrations/`。
- `keycloak`：`--import-realm` 掛載 `infra/k8s/keycloak/realm-export/`，啟動時自動匯入 `conotes` realm。這組匯入流程（含 `oidc-audience-mapper`）已經用這個版本（`26.7.3`）的 Keycloak 實際驗證過，走過一次完整的 Authorization Code + PKCE 登入拿到 access token、再用這個 token 打通本機 API 的 `/api/v1/notes`。
- `redis`：`collab-editing` change 加的，給 SignalR 的 Redis backplane 用（見上方「已知限制」）。
- API 本身沒有 Dockerfile，這裡故意不把它放進 docker-compose——用 `dotnet run --project src/CoNotes.Api`，並把 `ConnectionStrings:DefaultConnection`／`Authentication:Authority`／`OpenTelemetry:OtlpEndpoint`／`Redis:ConnectionString` 指向這組本機容器/服務即可，例如：

  ```bash
  ASPNETCORE_ENVIRONMENT=Development \
  ConnectionStrings__DefaultConnection="Host=localhost;Database=app;Username=conotes_app;Password=password" \
  Authentication__Authority="http://localhost:8081/realms/conotes" \
  OpenTelemetry__OtlpEndpoint="http://localhost:4318" \
  Redis__ConnectionString="localhost:6379" \
  dotnet run --project src/CoNotes.Api
  ```

  Keycloak 的 host port 故意配成 `8081`（不是 Keycloak 預設的 `8080`），因為 `8080` 被下面的本機 SigNoz UI 佔用了。

## 本機測試 PayPal webhook（ngrok）

`subscription-billing` change 的 license code 產生是由 PayPal 的 `PAYMENT.CAPTURE.COMPLETED` webhook 觸發（見 `openspec/changes/subscription-billing/design.md` 決定 2），webhook 需要 PayPal 的伺服器能打到一個公開可達的網址——本機開發用 [ngrok](https://ngrok.com/) 開一個對外隧道：

```bash
ngrok http 5094
```

拿到的公開網址（例如 `https://xxxx.ngrok-free.app`）需要向 PayPal 註冊成 webhook，訂閱 `PAYMENT.CAPTURE.COMPLETED` 事件：

```bash
curl -X POST https://api-m.sandbox.paypal.com/v1/notifications/webhooks \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://xxxx.ngrok-free.app/api/v1/billing/webhook",
    "event_types": [{"name": "PAYMENT.CAPTURE.COMPLETED"}]
  }'
```

回應裡的 `id` 就是 `PayPal:WebhookId`，簽章驗證(`IPayPalClient.TryVerifyCaptureCompletedEventAsync`)需要用到；連同 `PayPal:ClientId`／`PayPal:ClientSecret` 一起用環境變數帶給 `dotnet run`：

```bash
PayPal__ClientId="..." \
PayPal__ClientSecret="..." \
PayPal__WebhookId="..." \
dotnet run --project src/CoNotes.Api
```

**已知限制**：ngrok 免費版每次重啟都會拿到一個新的臨時網址，重啟後要重新呼叫 PayPal 的 webhook 更新 API（`PATCH /v1/notifications/webhooks/{id}`）把 `url` 換成新的，否則 PayPal 會繼續送到舊網址、本機收不到。也可以用 PayPal 的 webhook 模擬器（`POST /v1/notifications/simulate-event`）送一個真正簽過章的測試事件到目前的網址，不需要真的走一次付款流程就能驗證簽章驗證/送達是否正常——這個模擬器帶的是固定的假資料（`custom_id` 不是有效的 `PlanTier`），所以只能驗證「送達＋簽章驗證」，驗證不到「真的產生 code」這一段。

## 本機觀測性（SigNoz）

`infra/k8s/signoz/application.yaml` 部署的是官方 Helm chart（見上方部署順序第 4 步），本機開發沒有 k8s，所以不能直接套用同一份 manifest。SigNoz 官方現在也不再提供一份可以直接複製的 `docker-compose.yaml`——改用他們自己的 CLI `foundryctl` 動態產生整組 compose 檔（ClickHouse + otel-collector + query-service 等內部拓樸複雜，連官方都建議別手刻），這跟我們在 k8s 用官方 Helm chart、不手刻 raw manifest 是同一個理由。

```bash
curl -fsSL https://signoz.io/foundry.sh | bash   # 安裝 foundryctl（一次性）
cd infra/signoz-local
foundryctl cast -f casting.yaml                  # 產生並啟動本機 SigNoz（含 ClickHouse 等）
```

- 產生的檔案在 `infra/signoz-local/pours/`，是 `foundryctl` 的產出物，不進版本控制（見 `.gitignore`），要重建就重跑上面的指令；真正進版本控制的只有 `casting.yaml` 這份宣告式設定。
- 啟動後 SigNoz UI 在 `http://localhost:8080`，OTLP 接收端點在 `http://localhost:4317`（gRPC）/`4318`（HTTP）。
- **記憶體需求**：SigNoz 的 ClickHouse 等元件相當吃記憶體，本機 Docker VM（例如 Colima）分配 ~2GB 時會觸發 OOM，容器被系統強制殺掉、重啟；分配到 6GB 後這個問題消失，容器都能穩定 Healthy。官方文件本身建議至少 4GB。
- **OTLP 要用 HTTP/protobuf，不是 gRPC 預設值**：`OpenTelemetry.Exporter.OpenTelemetryProtocol` 的 gRPC 傳輸對非 TLS 的本機/內部端點會出現 `An HTTP/2 connection could not be established because the server did not complete the HTTP/2 handshake`，連不上——這不是本機限定的問題，我們整個架構的內部流量本來就是明碼（TLS 在 Cloudflare/nginx 這層就終止了），所以 k8s 環境下 API 打 SigNoz 的 otel-collector 也會遇到同樣的狀況。`src/CoNotes.Api/Program.cs` 已改成 `OtlpExportProtocol.HttpProtobuf`（端點對應改成 `4318`，路徑補上 `/v1/traces`、`/v1/logs`），這是純 HTTP POST、沒有這個限制，`appsettings.json` 與 `infra/k8s/api/configmap.yaml` 的預設值也一併從 `4317` 改成 `4318`。
- **已知未解問題**：把記憶體提高到 6GB 之後，改用 HTTP/protobuf 的匯出也確認不再是「連線被拒」，但這個環境裡用 `foundryctl v0.2.17` 產生的 SigNoz 安裝，其 `ingester` 容器持續每 30 秒對 SigNoz 自己的 OpAmp 設定伺服器回報 `Server returned an error response`（`docker logs signoz-ingester-1`），懷疑因此導致 OTLP receiver 的實際設定沒有正確生效——實測對 `:4318` 送出真正的 export 請求會拿到「連線被建立但沒有回應」（`Empty reply from server`），ClickHouse 的 `signoz_traces.signoz_index_v3` 也持續是 0 筆。這看起來是這次 `foundryctl` 產生的本機 SigNoz 安裝本身的問題，不是本專案程式碼或設定的問題；還沒有找到根因，之後要嘛在真正的開發機上重試（可能是這台環境特有的狀況），要嘛換一個 `foundryctl` 版本重新產生一次。
