**Bounded Context**: Identity｜**Aggregate**: `AppUser`

## Why

後面每個階段（筆記 CRUD、共編、AI Chat、訂閱金流）都要仰賴一個能運作的 cluster 基礎——資料庫、身份驗證、觀測性——先存在。把這塊獨立成一個 change，可以在任何產品功能開發之前，單獨檢視與驗證這個基礎是否成立。

## What Changes

- 在 2 節點 k8s cluster 上建立 PostgreSQL，Keycloak 使用獨立的 database，但共用同一個 Postgres instance。
- 建立 Redis（先留給之後階段的 SignalR backplane 用，跟其他資料層一起建起來）。
- 部署 Keycloak 作為 OIDC 身份提供者；後端應用程式驗證 Keycloak 簽發的 token，而不是自己刻登入機制。
- 部署 SigNoz 作為 OpenTelemetry-native 的觀測性後端（trace + log）。
- 建立一個 .NET 10 API 專案骨架，串接 Keycloak 做身份驗證，並埋 OpenTelemetry 儀器化，資料匯出到 SigNoz。
- 用 `golang-migrate` CLI 建立應用程式 Postgres 資料庫的 schema migration 機制（Dapper 沒有內建的 migration 工具）。
- 部署 nginx（ingress-nginx）作為 cluster 內的反向代理／Ingress 進入點，依 hostname 路由到 API（`api.<domain>`）或 Keycloak（`auth.<domain>`）。
- 讓現有的 Cloudflare Tunnel 改指向 nginx 這一個內部進入點，取代原本要分別對應多個 Service 的做法。
- 讓既有的 ArgoCD Application（`infra/argocd/application.yaml`）真正同步 `infra/k8s` 底下的 manifests。

## Capabilities

### New Capabilities
- `identity/authentication`：使用者可以透過 Keycloak 簽發的 OIDC token 完成身份驗證；API 會驗證 token，拒絕未帶有效 token 呼叫受保護 endpoint 的請求。訂閱等級（Free/Pro/ProMax）明確不在這個 capability 的範圍內——它存在應用程式自己的資料庫，不存在 Keycloak，將在後面的階段引入。

### Modified Capabilities
（無 —— 這是這個專案的第一個 change）

## Impact

- 新增的 stateful k8s workload：PostgreSQL、Redis、Keycloak、SigNoz。
- 新的 repo 結構：`infra/k8s/` 下的 manifests（Postgres、Redis、Keycloak、SigNoz、nginx、API）、`golang-migrate` 用的 migrations 目錄、以及一個新的 .NET 10 API 專案。
- Cloudflare Tunnel 設定改為只指向 nginx，由 nginx 的 Ingress 規則分流到 API（`api.<domain>`）跟 Keycloak（`auth.<domain>`）。
- ArgoCD 現在有真正的 manifests 可以同步到 `infra/k8s`（先前是空的）。
- 不包含任何產品面功能（筆記、共編、AI Chat、金流）——這些都是後面獨立的 change。
