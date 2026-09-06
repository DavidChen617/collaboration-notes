## 1. 密鑰管理腳本

- [x] 1.1 撰寫 `infra/scripts/apply-secrets.sh`：讀取伺服器端指定路徑的 `.env` 檔案，用 `kubectl create secret generic ... --from-env-file --dry-run=client -o yaml | kubectl apply -f -` 的方式 upsert 一個 k8s Secret
- [x] 1.2 在 repo 說明文件（例如 README 或 `infra/` 底下的說明檔）記錄：`.env` 檔案該放在伺服器的哪個路徑、包含哪些變數（PayPal sandbox key、AI provider key、Keycloak client secret 等）、什麼時候需要重新執行這支腳本
- [x] 1.3 在乾淨的測試環境手動跑一次腳本，驗證產生的 k8s Secret 內容跟 `.env` 一致——已對真的 3-node 叢集驗證：套用真的 PayPal 憑證後，用 SHA-256 雜湊比對 Secret 內容與來源 `.env` 逐 key 完全一致

## 2. PR Workflow（測試）

- [x] 2.1 新增 `.github/workflows/pr-checks.yml`，PR 觸發時跑 `dotnet test CoNotes.slnx`（涵蓋 Unit/Integration/Functional/Architecture 四個測試專案）
- [ ] 2.2 確認 Integration/Functional 測試依賴的 Testcontainers 能在 GitHub-hosted runner 上正常啟動 Postgres 容器——本地已用相同指令跑過 135 個測試全過（含需要真的 PayPal sandbox 憑證的 5 個），但這是本機 Docker daemon，還沒在真的 GitHub-hosted runner 上跑過，等 3.5 一起驗證
- [x] 2.3 在同一個 workflow 加上 `CoNotes.Client` 的建置步驟（`pnpm install` + `ng build`）
- [ ] 2.4 開一個測試用 PR，驗證四種測試與前端建置都會被觸發，且刻意讓某個測試失敗一次，確認 PR 會被標記為失敗

> **實作過程中發現的缺口（design.md 沒提到）：** `PayPalClientTests`/`BillingEndpointTests` 有 5 個測試打真的 PayPal sandbox API（專案既有慣例：外部依賴沒準備好就讓測試失敗，不靜默略過），CI runner 上沒有這些憑證會讓所有 PR 永遠卡住。已跟你確認後，把 PayPal sandbox 的 ClientId/ClientSecret/WebhookId 加進 repo 的 Actions Secrets（`PAYPAL_CLIENT_ID`/`PAYPAL_CLIENT_SECRET`/`PAYPAL_WEBHOOK_ID`），並在 `pr-checks.yml`/`deploy.yml` 的 test job 注入成 `PayPal__*` 環境變數；本地用同樣的環境變數重跑過，135/135 全過。

## 3. Main Workflow（建置、推送、更新部署）

- [x] 3.1 新增 `.github/workflows/deploy.yml`，觸發條件為 push 到 main、且排除 `infra/k8s/**` 路徑
- [x] 3.2 實作建置 `CoNotes.Api` container image、以 git SHA 當 tag，推送到 ghcr.io
- [ ] 3.3 確認 repo 的 Actions 設定已開啟 `contents: write` 權限，讓 workflow 能用 `GITHUB_TOKEN` commit 回 main——已用 `gh api repos/{owner}/{repo}/actions/permissions/workflow` 查到目前是 `read`，需要改成 `write`；改這個設定的 API 呼叫被 Claude Code 的權限分類器擋下（會動到 repo 帳號設定），需要你自己去 repo Settings → Actions → General → Workflow permissions 手動切成「Read and write permissions」
- [x] 3.4 實作更新 `infra/k8s` 底下 API Deployment manifest 的 image tag、commit 回 main 的步驟
- [ ] 3.5 驗證合併一個改動 `CoNotes.Api` 的 PR 後，main workflow 會自動跑完建置、推送、commit 這三步，且不會觸發第二次 workflow（驗證 `paths-ignore` 生效）——需要 3.3 的權限設定先處理好，且要先把目前的本地 commit 推上 GitHub
- [ ] 3.6 驗證 ArgoCD 在 manifest 被更新後，`selfHeal` 自動同步、`CoNotes.Api` 的 pod 滾動更新到新版本

## 4. GitHub Pages 部署

- [x] 4.1 在 main workflow 加上建置 `CoNotes.Client`、部署靜態輸出到 GitHub Pages 的步驟——`ng build --base-href /collaboration-notes/`（對應改名後的 repo 名稱）已在本地驗證過，輸出的 `index.html` 確實帶有正確的 `<base href>`
- [ ] 4.2 驗證部署完成後，GitHub Pages 網址可以正常開啟、內容是最新版本——需要先確認 repo 的 GitHub Pages 來源設為「GitHub Actions」，且要有一次真的 workflow 跑完

## 5. 端對端驗證

- [ ] 5.1 完整跑一次流程：改一行 `CoNotes.Api` 的程式碼 → 開 PR（確認測試跑過）→ 合併進 main → 確認 image 推送、manifest 更新、ArgoCD 同步、pod 更新到新版本全部發生
- [ ] 5.2 完整跑一次流程：改一行 `CoNotes.Client` 的程式碼 → 合併進 main → 確認 GitHub Pages 更新成功
- [ ] 5.3 在一個乾淨環境，依 1.2 記錄的文件步驟重建密鑰（跑 `apply-secrets.sh`），確認流程本身文件寫得夠清楚、不用額外詢問就能照做
