**Bounded Context**: Billing｜**Aggregate**: `LicenseCode`（同時修改 Identity 的 `AppUser`、Notes 的 `Note`/`ChatMessage`）

## Why

`collab-editing`（Pro）跟 `ai-chat`（ProMax）目前對所有使用者開放，沒有訂閱等級限制——這個 change 補上訂閱付款與等級解鎖機制，把這兩個功能真正變成付費分級的樣子。

## What Changes

- 使用者可以透過 PayPal Subscriptions API（sandbox）訂閱 Pro 或 ProMax 等級。
- PayPal 訂閱首次啟用成功後，系統產生一組對應等級的 license code。
- 使用者輸入尚未使用過的 license code 後，系統把該使用者的訂閱等級更新為 code 對應的等級——這是**一次性解鎖**：之後 PayPal 那邊扣款失敗或取消訂閱，都不會自動改變已經兌換生效的等級。
- 系統管理者可以手動把使用者的訂閱等級撤銷回 Free（因為等級不會隨 PayPal 扣款狀態自動變動，需要一個獨立的撤銷手段）。
- `collab-editing` 的「產生分享連結」動作，改為只有筆記擁有者的訂閱等級是 Pro 或以上才能使用。
- `ai-chat` 功能，改為只有筆記擁有者的訂閱等級是 ProMax 才能使用。
- 兩者都只檢查**筆記擁有者**的等級——透過分享連結加入的共編者，不需要自己也有對應訂閱才能參與協作或聊天。

## Capabilities

### New Capabilities
- `subscription-billing`：使用者可以透過 PayPal 訂閱取得 license code，兌換 code 解鎖對應的訂閱等級；系統管理者可以手動撤銷等級。

### Modified Capabilities
- `collab-editing`：「產生分享連結」這個既有需求，改為要求筆記擁有者的訂閱等級為 Pro 以上。
- `ai-chat`：聊天室相關的既有需求，改為要求筆記擁有者的訂閱等級為 ProMax。

## Impact

- 新增 Postgres schema：`LicenseCode`（code、對應等級、兌換狀態、兌換者）；`AppUser` 新增 `PlanTier` 欄位。
- 新增 API：建立 PayPal 訂閱 checkout、PayPal webhook 接收端點、兌換 license code、管理者撤銷等級。
- 調整既有 API 的存取檢查：`collab-editing` 的分享連結產生邏輯、`ai-chat` 的聊天室相關邏輯，都要多檢查筆記擁有者目前的 `PlanTier`。
- **依賴前提**：這個 change 修改 `collab-editing` 與 `ai-chat` 的既有需求，實作順序上應該排在這兩者都完成之後。
