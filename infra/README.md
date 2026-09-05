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
- **`Redis` 尚未部署**：`proposal.md` 提到要跟其他資料層一起建起來（給之後 SignalR backplane 用），但 `tasks.md` 目前沒有對應的 task item，這個 change 因此還沒有 Redis 的 manifest。

## 本機開發（docker-compose）

不需要 k8s cluster，`docker-compose.yaml`（repo 根目錄）啟動 Postgres + migration + Keycloak 這組資料層/驗證層，API 本身用 `dotnet run` 在本機跑，指向這組容器：

```bash
docker compose up -d
```

- `postgres`：對應 `infra/k8s/postgres/` 的邏輯，`infra/postgres-init/` 底下的 init script 建立 `app`、`keycloak` 兩個 database（密碼都是明碼 `password`，僅供本機開發用）。
- `migrate`：用官方 `migrate/migrate` image 對 `app` database 套用 `src/CoNotes.Infrastructure/Persistence/Migrations/`。
- `keycloak`：`--import-realm` 掛載 `infra/k8s/keycloak/realm-export/`，啟動時自動匯入 `conotes` realm。這組匯入流程（含 `oidc-audience-mapper`）已經用這個版本（`26.7.3`）的 Keycloak 實際驗證過，走過一次完整的 Authorization Code + PKCE 登入拿到 access token、再用這個 token 打通本機 API 的 `/api/v1/notes`。
- API 本身沒有 Dockerfile，這裡故意不把它放進 docker-compose——用 `dotnet run --project src/CoNotes.Api`，並把 `ConnectionStrings:DefaultConnection`／`Authentication:Authority`／`OpenTelemetry:OtlpEndpoint` 指向這組本機容器/服務即可，例如：

  ```bash
  ASPNETCORE_ENVIRONMENT=Development \
  ConnectionStrings__DefaultConnection="Host=localhost;Database=app;Username=conotes_app;Password=password" \
  Authentication__Authority="http://localhost:8081/realms/conotes" \
  OpenTelemetry__OtlpEndpoint="http://localhost:4317" \
  dotnet run --project src/CoNotes.Api
  ```

  Keycloak 的 host port 故意配成 `8081`（不是 Keycloak 預設的 `8080`），因為 `8080` 被下面的本機 SigNoz UI 佔用了。

## 本機觀測性（SigNoz）

`infra/k8s/signoz/application.yaml` 部署的是官方 Helm chart（見上方部署順序第 4 步），本機開發沒有 k8s，所以不能直接套用同一份 manifest。SigNoz 官方現在也不再提供一份可以直接複製的 `docker-compose.yaml`——改用他們自己的 CLI `foundryctl` 動態產生整組 compose 檔（ClickHouse + otel-collector + query-service 等內部拓樸複雜，連官方都建議別手刻），這跟我們在 k8s 用官方 Helm chart、不手刻 raw manifest 是同一個理由。

```bash
curl -fsSL https://signoz.io/foundry.sh | bash   # 安裝 foundryctl（一次性）
cd infra/signoz-local
foundryctl cast -f casting.yaml                  # 產生並啟動本機 SigNoz（含 ClickHouse 等）
```

- 產生的檔案在 `infra/signoz-local/pours/`，是 `foundryctl` 的產出物，不進版本控制（見 `.gitignore`），要重建就重跑上面的指令；真正進版本控制的只有 `casting.yaml` 這份宣告式設定。
- 啟動後 SigNoz UI 在 `http://localhost:8080`，OTLP 接收端點在 `http://localhost:4317`（gRPC）/`4318`（HTTP）——對應到上一節 `dotnet run` 範例裡的 `OpenTelemetry__OtlpEndpoint`。
- **已知限制／資源需求**：SigNoz 的 ClickHouse 等元件相當吃記憶體，如果同時跑 SigNoz + 本專案自己的 docker-compose（Postgres/Keycloak）+ API，在記憶體有限的機器上（例如 Docker/Colima 只分配 ~2GB）會觸發 OOM，容器被系統強制殺掉、重啟。這個限制在資源有限的開發環境下實際發生過（容器在跑不到一分鐘內被殺掉、ClickHouse 遲遲跑不完 schema migration），本機 Docker/Colima 需要分配足夠記憶體（建議至少 4GB，官方文件本身也要求至少 4GB）才能穩定驗證這條 trace 路徑；資源不夠時，SigNoz 服務本身仍然啟動成功，只是還沒能完整走一次「API 呼叫 → trace 出現在 SigNoz」的端對端驗證。
