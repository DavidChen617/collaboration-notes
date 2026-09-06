## MODIFIED Requirements

### Requirement: 筆記擁有者可以產生分享連結，讓已登入的使用者加入共編
筆記擁有者的訂閱等級為 Pro 以上時，SHALL 能為自己的筆記產生一條帶有唯一識別碼的分享連結；已登入的使用者開啟有效的分享連結時，系統 SHALL 將其加入這篇筆記的共編者名單。筆記擁有者的訂閱等級為 Free 時，系統 SHALL 拒絕產生分享連結。

**Path**: Command（`GenerateShareLinkCommand`，Notes context；等級檢查是在 Command Handler 執行前，透過同步查詢 Identity context 的 `AppUser.PlanTier` 做的前置條件檢查，不是事件，也不修改 `Note` Aggregate 以外的任何狀態）

#### Scenario: 產生分享連結
- **WHEN** 訂閱等級為 Pro 以上的筆記擁有者，為自己的筆記產生一條分享連結
- **THEN** 系統回傳一條帶有唯一識別碼的分享連結

#### Scenario: 已登入使用者透過分享連結加入共編
- **WHEN** 一個已登入的使用者開啟這篇筆記目前有效的分享連結
- **THEN** 系統將該使用者加入這篇筆記的共編者名單

#### Scenario: 同一條連結可被多人使用
- **WHEN** 另一個不同的已登入使用者，開啟同一條仍然有效的分享連結
- **THEN** 系統同樣將這個使用者加入共編者名單，不影響先前已加入的共編者

#### Scenario: Free 等級的擁有者嘗試產生分享連結
- **WHEN** 訂閱等級為 Free 的筆記擁有者，嘗試為自己的筆記產生分享連結
- **THEN** 系統拒絕此次請求，不產生分享連結
