## Purpose

讓使用者可以透過 PayPal 訂閱付款取得對應等級的 license code，兌換後一次性解鎖 Pro 或 ProMax 等級；等級一旦兌換生效，不隨後續 PayPal 扣款狀態自動變動，需要系統管理者手動撤銷。

## ADDED Requirements

### Requirement: 使用者透過 PayPal 訂閱取得對應等級的 license code
使用者透過 PayPal 完成一筆訂閱付款、且該訂閱首次啟用成功時，系統 SHALL 產生一組對應該訂閱等級、尚未使用過的 license code。

**Path**: Command（`IssueLicenseCodeCommand`，由 PayPal webhook 觸發，發出 `LicenseCodeIssued`）

#### Scenario: 訂閱付款成功後產生 license code
- **WHEN** 使用者透過 PayPal 完成一筆 Pro 或 ProMax 等級的訂閱付款，且該訂閱首次啟用成功
- **THEN** 系統產生一組對應該等級、尚未使用過的 license code

### Requirement: 使用者可以兌換有效的 license code
已登入的使用者 SHALL 能輸入一組尚未使用過的有效 license code，兌換後系統 SHALL 將該使用者的訂閱等級更新為該 code 對應的等級。

**Path**: Command（`RedeemLicenseCodeCommand`，發出跨 Context 的 `LicenseCodeRedeemed`，由 Identity context 監聽並更新 `AppUser.PlanTier`）

#### Scenario: 兌換有效的 license code
- **WHEN** 已登入的使用者輸入一組尚未使用過的有效 license code
- **THEN** 系統將該使用者的訂閱等級更新為該 code 對應的等級

### Requirement: 已兌換過的 license code 不能被再次兌換
系統 SHALL 拒絕兌換已經被使用過的 license code。

**Path**: Command（`RedeemLicenseCodeCommand` 內的拒絕分支，屬於 `LicenseCode` Aggregate 自身的不變條件）

#### Scenario: 嘗試兌換已使用過的 code
- **WHEN** 使用者輸入一組已經被兌換過的 license code
- **THEN** 系統拒絕此次兌換，該使用者的訂閱等級不變

### Requirement: 已兌換生效的訂閱等級不隨 PayPal 後續狀態自動變動
使用者兌換 license code 取得的訂閱等級，SHALL NOT 因為對應 PayPal 訂閱後續被取消或扣款失敗而被系統自動調整。

**Path**: Command（不變條件——描述的是撤銷/變更等級這個 Command 路徑*不會*被觸發，而非一個獨立的讀寫動作；系統只監聽「訂閱首次啟用成功」事件，PayPal 後續事件完全不會分派任何 Command）

#### Scenario: PayPal 訂閱後續狀態變化不影響已兌換的等級
- **WHEN** 使用者已經兌換 code 取得 Pro 或 ProMax 等級之後，其原本對應的 PayPal 訂閱被取消或扣款失敗
- **THEN** 該使用者的訂閱等級維持不變，不會被系統自動調整

### Requirement: 系統管理者可以手動撤銷使用者的訂閱等級
系統管理者 SHALL 能將任一使用者的訂閱等級手動撤銷回 Free。

**Path**: Command（`RevokeAppUserPlanTierCommand`，發出 `AppUserPlanTierRevoked`；發生在 Identity context 內部，不需要跨 Context 事件）

#### Scenario: 管理者撤銷使用者的等級
- **WHEN** 系統管理者對某個使用者執行撤銷訂閱等級的操作
- **THEN** 該使用者的訂閱等級被設回 Free
