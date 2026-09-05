# CoNotes（共編筆記）

一個即時共編筆記產品：建立筆記、用雙向連結（wikilink）把筆記串起來、邀請他人共同編輯，並在筆記裡跟 AI 對話。玩具/學習專案，非商業應用。名詞定義見 [CONTEXT.md](./CONTEXT.md)。

## 功能分級

- **Free**：筆記的建立、編輯、刪除、wikilink 連結、關係圖檢視
- **Pro**：+ 分享連結即時共編（多人同時編輯同一篇筆記）
- **ProMax**：+ 筆記內建 AI 聊天室（`@AI` 觸發）

等級透過 PayPal 訂閱付款取得的授權碼兌換解鎖，兌換後與後續付款狀態脫鉤（一次性生效）。

## 技術棧

- **後端**：.NET 10 Web API（Minimal API）、PostgreSQL + Dapper、`golang-migrate` 做 schema migration
- **身份驗證**：Keycloak（OIDC），API: JWT Bearer Resource Server
- **即時通訊**：SignalR（共編、聊天室各自獨立 Hub）+ Redis backplane
- **前端**：Angular SPA（`src/CoNotes.Client/`，部署在 GitHub Pages）、Tiptap（含 Yjs 協作擴充）做編輯器、Cytoscape.js 做關係圖
- **AI Chat**：多個免費額度 AI provider，Chain of Responsibility 依序 failover
- **觀測性**：SigNoz（OpenTelemetry-native，trace + log + metric）
- **部署**：2 節點 k8s cluster、nginx 當內部反向代理、Cloudflare Tunnel 對外、ArgoCD 做 GitOps
- **金流**：PayPal Subscriptions API（sandbox）

## 架構

DDD + CQRS，4 層 + 3 個 Bounded Context：

```
Domain          Entity / Aggregate / Domain Event / Repository 介面
Application     Command（寫入，經 Aggregate + Repository）／Query（直接 Dapper 查詢）
Infrastructure  Repository 實作、外部服務整合（Keycloak、PayPal、AI provider、SignalR）
Api             Minimal API Endpoint，只做輸入驗證、分派給 Handler
```

| Bounded Context | Aggregate             | 專案                                                     |
| --------------- | --------------------- | -------------------------------------------------------- |
| Identity        | `AppUser`             | `src/CoNotes.Domain` 等（跟其他 context 共用同一組專案） |
| Notes           | `Note`、`ChatMessage` | 同上                                                     |
| Billing         | `LicenseCode`         | 同上                                                     |

專案結構是扁平的單一服務佈局：`src/CoNotes.Domain`、`src/CoNotes.Application`、`src/CoNotes.Infrastructure`、`src/CoNotes.Api`、`src/CoNotes.Client`；測試對應 `tests/CoNotes.UnitTests`、`IntegrationTests`、`FunctionalTests`、`ArchitectureTests`。完整的分層命名規則、DDD 慣例、測試慣例記在 [`openspec/config.yaml`](./openspec/config.yaml)，`sample/` 是套用同一套慣例的完整參考範例（Todo 服務）。

## 目前進度

功能規劃已完成，尚未開始實作。七個 change 依序記錄在 `openspec/changes/`：

1. `setup-infra-and-auth` — k8s 基礎設施、Keycloak 身份驗證
2. `notes-crud` — 筆記 CRUD（Free）
3. `note-linking` — wikilink 連結、關係圖
4. `collab-editing` — 分享連結即時共編（Pro）
5. `ai-chat` — 筆記內建 AI 聊天室（ProMax）
6. `subscription-billing` — PayPal 訂閱、授權碼兌換、分級限制
7. `cicd-deployment` — CI/CD 建置、測試、部署流程

每個 change 底下的 `proposal.md`/`design.md`/`tasks.md` 記錄了完整的決策與取捨理由。
