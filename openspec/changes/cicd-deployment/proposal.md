**Bounded Context**: 無（純 CI/CD 工具鏈與部署流程，不涉及任何業務 Aggregate，`skip_specs: true`）

## Why

目前 `infra/argocd/application.yaml` 已經指向 `infra/k8s`，但沒有任何流程負責「測試通過 → 建置 image → 推送 → 更新部署 manifest」這一段，等於 ArgoCD 有 GitOps 的目的地，卻沒有自動化的來源。這個 change 補上 CI/CD，讓 push 到 main 之後能自動跑完測試、建置、部署，不用每次手動操作。

## What Changes

- 新增 GitHub Actions workflow（PR 觸發）：跑 `CoNotes.slnx` 的四種測試專案（Unit/Integration/Functional/Architecture，Integration/Functional 需要 Testcontainers 用到的 Docker）、建置 Angular 專案。
- 新增 GitHub Actions workflow（push 到 main 觸發，排除只改 `infra/k8s/**` 的 commit，避免自我觸發的無窮迴圈）：
  - 建置 `CoNotes.Api` 的 container image，以 git SHA 當 tag，推到 GitHub Container Registry（ghcr.io）
  - 更新 `infra/k8s` 底下 API Deployment 的 image tag，commit 回 main（ArgoCD 的 `selfHeal` 會自動同步）
  - 建置 `CoNotes.Client`，把靜態輸出部署到 GitHub Pages
- **密鑰不進 CI/CD、也不進 git**：PayPal/AI provider API key、Keycloak client secret 等，維護在伺服器端的一份 `.env` 檔案，透過一支手動執行的腳本（`infra/scripts/apply-secrets.sh`）套用成 k8s Secret；CI/CD 完全不碰這塊。

## Capabilities

（無 —— 這是純工具鏈/部署流程的變更，不影響任何使用者可觀察的行為，`skip_specs: true`）

## Impact

- 新增 `.github/workflows/` 底下至少兩個 workflow 檔案。
- 新增 `infra/scripts/apply-secrets.sh`，需要文件說明使用時機（首次建置環境、密鑰輪替時）。
- `infra/k8s` 底下 API 的 Deployment manifest 會被 CI 自動改動（image tag），人工修改這個檔案時要留意可能被下一次 CI 覆蓋。
- 需要在 GitHub repo 設定允許 Actions 用內建 `GITHUB_TOKEN` 寫回 repo 內容（`contents: write` 權限）。
