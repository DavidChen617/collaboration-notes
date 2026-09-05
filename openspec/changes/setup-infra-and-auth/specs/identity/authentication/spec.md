## Purpose

讓使用者能透過外部 OIDC 身份提供者完成登入驗證，並讓系統其他部分能以一致、可信賴的方式確認每個請求背後的使用者身份，同時為每個已驗證的使用者維護對應的應用程式內部使用者記錄。

## ADDED Requirements

### Requirement: 使用者透過 OIDC 完成登入驗證
使用者 SHALL 能透過外部 OIDC 身份提供者完成登入，並取得可用來呼叫受保護 API 的存取憑證（access token）。

**Path**: Command（登入成功會觸發 `UpsertAppUserCommand`，屬於寫入路徑）

#### Scenario: 登入成功並取得有效憑證
- **WHEN** 使用者完成身份提供者的登入流程，取得一組有效的存取憑證
- **THEN** 該使用者可用此憑證呼叫受保護的 API endpoint 並取得正常回應

### Requirement: 系統拒絕未通過驗證的請求
API SHALL 拒絕任何未攜帶有效存取憑證的請求存取受保護資源。

**Path**: 橫切關注點（Cross-cutting）——以 middleware 形式套用在所有受保護的 Command 與 Query endpoint，不專屬單一路徑

#### Scenario: 未攜帶憑證呼叫受保護 endpoint
- **WHEN** 請求呼叫受保護的 API endpoint，且未攜帶任何存取憑證
- **THEN** 系統回傳未授權錯誤，且不執行該 endpoint 的邏輯

#### Scenario: 攜帶過期或簽章無效的憑證
- **WHEN** 請求攜帶的存取憑證已過期，或其簽章無法通過驗證
- **THEN** 系統回傳未授權錯誤，且不執行該 endpoint 的邏輯

### Requirement: 系統為已驗證使用者維護對應的應用程式使用者記錄
系統 SHALL 為每個成功通過驗證的使用者，維護一筆對應的應用程式內部使用者記錄，供其他功能（如筆記歸屬、訂閱等級）關聯使用。

**Path**: Command（`UpsertAppUserCommand`，寫入 `AppUser` Aggregate）

#### Scenario: 使用者第一次通過驗證
- **WHEN** 一個先前沒有對應應用程式使用者記錄的已驗證使用者，第一次成功呼叫受保護 API
- **THEN** 系統建立一筆新的應用程式使用者記錄，並將其與該使用者的身份識別碼關聯

#### Scenario: 既有使用者再次呼叫
- **WHEN** 一個已經有對應應用程式使用者記錄的已驗證使用者，再次成功呼叫受保護 API
- **THEN** 系統使用既有的應用程式使用者記錄，不重複建立新記錄

### Requirement: 健康檢查 endpoint 不需要身份驗證
API SHALL 提供至少一個不需要身份驗證即可呼叫的健康檢查 endpoint，供叢集的存活/就緒探測使用。

**Path**: Query（唯讀回傳健康狀態，不涉及任何 Aggregate）

#### Scenario: 叢集探測呼叫健康檢查 endpoint
- **WHEN** 呼叫健康檢查 endpoint，且未攜帶任何存取憑證
- **THEN** 系統回傳服務目前的健康狀態，不要求身份驗證
