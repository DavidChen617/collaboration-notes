# 部署說明

記錄 `setup-infra-and-auth` 這個 change 的部署順序與已知限制，供之後重建環境參考。完整的決策理由見
[`openspec/changes/setup-infra-and-auth/design.md`](../openspec/changes/setup-infra-and-auth/design.md)。

## 部署順序

1. **Postgres**（`deploy/k8s/postgres/`）：單一 instance，透過 init script（`configmap-init.yaml`）在第一次啟動時建立 `app`、`keycloak` 兩個 database 與對應帳號。
2. **Migration**：用 `golang-migrate` 對 `app` database 套用 `src/CoNotes.Infrastructure/Persistence/Migrations/` 底下的 SQL，建立 `app_users` table。
3. **Keycloak**（`deploy/k8s/keycloak/`）：指向 `keycloak` database；`--import-realm` 會在啟動時自動匯入 `realm-export/conotes-realm.json` 定義的 Realm 與 public client。
4. **SigNoz**（`deploy/k8s/signoz/application.yaml`）：以官方 Helm chart 部署（child ArgoCD Application）。
5. **ingress-nginx controller**（`deploy/k8s/ingress-nginx/application.yaml`）：以官方 Helm chart 部署（child ArgoCD Application）。
6. **API**（`deploy/k8s/api/`）：JWT Bearer 的 `Authority` 指向 Keycloak Realm、OTLP exporter 指向 SigNoz 的 otel-collector。
7. **Ingress 規則**（`deploy/k8s/ingress/ingress.yaml`）：`api.<domain>` → API Service、`auth.<domain>` → Keycloak Service。
8. **Cloudflare Tunnel**（`deploy/cloudflared/config.yml`）：把單一目標指向 ingress-nginx controller 的 Service（這一步在 repo 之外，透過 Cloudflare Zero Trust 操作)。
9. **ArgoCD**（`deploy/argocd/application.yaml`）：指向 `deploy/k8s`，`directory.recurse: true` 讓它能找到巢狀資料夾裡的 manifest。

## 已知限制

- **單副本 Keycloak**：沒有用官方 Operator，也沒有多副本；它是單點故障，掛掉之後沒人能登入（既有的 access token 在過期前仍可正常呼叫 API）。
- **Keycloak 用 `start-dev`**：為求簡化，不是生產模式（`start`）。可接受，因為這個 change 明確排除生產級硬化／HA。
- **共用同一個 Postgres instance**：`app`、`keycloak` 是同一個 instance 上的兩個獨立 database，不是獨立 instance；這個 instance 掛掉會同時拖垮登入跟 App 資料。用獨立 database（不是 schema）保留了之後切到獨立 instance 的路徑。
- **所有 Secret 都是明碼佔位密碼**：這是玩具/學習專案，`stringData` 直接寫死方便重建環境；正式使用前必須替換。
- **沒有設定任何 pod resource requests/limits**：design.md 的 Open Question，留到實際觀察用量後再調整。
- **`realm-export/conotes-realm.json` 未經過真的 Keycloak 匯入/匯出驗證**：內容依 Keycloak 官方 realm-export 格式撰寫，但撰寫當下沒有可用的 Keycloak 實例可以驗證匯入後的一致性，請在有 Keycloak 的環境重新驗證一次。
- **`Redis` 尚未部署**：`proposal.md` 提到要跟其他資料層一起建起來（給之後 SignalR backplane 用），但 `tasks.md` 目前沒有對應的 task item，這個 change 因此還沒有 Redis 的 manifest。
