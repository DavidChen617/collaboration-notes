## MODIFIED Requirements

### Requirement: 擁有者與共編者可以在筆記聊天室傳送訊息
筆記擁有者的訂閱等級為 ProMax 時，該筆記的擁有者與共編者 SHALL 能在該筆記的聊天室傳送訊息，且訊息 SHALL 讓其他有權存取該聊天室的人看到。筆記擁有者的訂閱等級不是 ProMax 時，系統 SHALL 拒絕該筆記聊天室的訊息存取。

**Path**: Command（`SendChatMessageCommand`，Notes context；等級檢查是在 Command Handler 執行前，透過同步查詢 Identity context 的 `AppUser.PlanTier` 做的前置條件檢查，不是事件，也不修改 `ChatMessage` Aggregate 以外的任何狀態）

#### Scenario: 擁有者傳送訊息
- **WHEN** 訂閱等級為 ProMax 的筆記擁有者，在該筆記的聊天室傳送一則訊息
- **THEN** 該訊息被儲存，且該筆記所有有權存取聊天室的使用者都能看到

#### Scenario: 共編者傳送訊息
- **WHEN** 一篇訂閱等級為 ProMax 的筆記，其共編者在聊天室傳送一則訊息
- **THEN** 該訊息被儲存，且該筆記所有有權存取聊天室的使用者都能看到

#### Scenario: 擁有者等級不是 ProMax 時聊天室不可用
- **WHEN** 訂閱等級不是 ProMax 的筆記擁有者或其共編者，嘗試存取該筆記的聊天室
- **THEN** 系統拒絕該請求
