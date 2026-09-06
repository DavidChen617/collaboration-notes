# 部署說明

記錄 `setup-infra-and-auth` 這個 change 的部署順序與已知限制，供之後重建環境參考。完整的決策理由見
[`openspec/changes/setup-infra-and-auth/design.md`](../openspec/changes/setup-infra-and-auth/design.md)。

## 部署順序

1. **Postgres**（`infra/k8s/postgres/`）：單一 instance，透過 init script（`configmap-init.yaml`）在第一次啟動時建立 `app`、`keycloak` 兩個 database 與對應帳號。
2. **Migration**：用 `golang-migrate` 對 `app` database 套用 `src/CoNotes.Infrastructure/Persistence/Migrations/` 底下的 SQL，建立 `app_users` table。
3. **Keycloak**（`infra/k8s/keycloak/`）：指向 `keycloak` database；`--import-realm` 會在啟動時自動匯入 `realm-export/conotes-realm.json` 定義的 Realm 與 public client。
4. **SigNoz**（`infra/k8s/signoz/application.yaml`）：以官方 Helm chart 部署（child ArgoCD Application）。
5. **ingress-nginx controller**（`infra/k8s/ingress-nginx/application.yaml`）：以官方 Helm chart 部署（child ArgoCD Application）。
6. **API**（`infra/k8s/api/`）：JWT Bearer 的 `Authority` 指向 Keycloak Realm、OTLP exporter 指向 SigNoz 的 otel-collector。
7. **Ingress 規則**（`infra/k8s/ingress/ingress.yaml`）：`api.<domain>` → API Service、`auth.<domain>` → Keycloak Service。
8. **Cloudflare Tunnel**（`infra/cloudflared/config.yml`）：把單一目標指向 ingress-nginx controller 的 Service（這一步在 repo 之外，透過 Cloudflare Zero Trust 操作)。
9. **ArgoCD**（`infra/argocd/application.yaml`）：指向 `infra/k8s`，`directory.recurse: true` 讓它能找到巢狀資料夾裡的 manifest。

## 已知限制

- **單副本 Keycloak**：沒有用官方 Operator，也沒有多副本；它是單點故障，掛掉之後沒人能登入（既有的 access token 在過期前仍可正常呼叫 API）。
- **Keycloak 用 `start-dev`**：為求簡化，不是生產模式（`start`）。可接受，因為這個 change 明確排除生產級硬化／HA。
- **共用同一個 Postgres instance**：`app`、`keycloak` 是同一個 instance 上的兩個獨立 database，不是獨立 instance；這個 instance 掛掉會同時拖垮登入跟 App 資料。用獨立 database（不是 schema）保留了之後切到獨立 instance 的路徑。
- **所有 Secret 都是明碼佔位密碼**：這是玩具/學習專案，`stringData` 直接寫死方便重建環境；正式使用前必須替換。
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
