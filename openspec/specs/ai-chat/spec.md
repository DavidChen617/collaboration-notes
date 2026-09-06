## Purpose

讓筆記的擁有者與共編者可以在該筆記的聊天室互相傳訊息，並可用 `@AI` 觸發一個能參考聊天歷史與筆記內容的 AI 助理加入對話，即使某個 AI provider 無法回應也能透過備援機制取得回覆。

## Requirements

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

### Requirement: 非擁有者且非共編者無法存取聊天室
系統 SHALL 拒絕既非擁有者、也非共編者的使用者讀取或傳送某篇筆記的聊天室訊息。

**Path**: 橫切關注點（Cross-cutting）——同時套用在讀取歷史訊息（Query）與傳送訊息（Command）兩條路徑，不專屬單一路徑

#### Scenario: 無關使用者嘗試存取聊天室
- **WHEN** 一個既非擁有者、也非共編者的使用者嘗試讀取或傳送這篇筆記的聊天室訊息
- **THEN** 系統拒絕該請求

### Requirement: 使用者可以用 @AI 觸發 AI 回覆
當訊息內容包含獨立的 `@AI` 字詞時，系統 SHALL 觸發 AI 產生一則回覆，並將回覆以聊天訊息的形式加入同一個聊天室。

**Path**: Command

#### Scenario: 訊息中標記 @AI
- **WHEN** 使用者傳送的訊息中包含獨立的 `@AI` 字詞
- **THEN** 系統觸發 AI 產生一則回覆，並將回覆以聊天訊息的形式加入同一個聊天室，讓該聊天室的所有參與者都能看到

### Requirement: AI 回覆會參考聊天歷史與筆記內容
AI 產生回覆時，其依據 SHALL 包含這篇筆記目前的內容，以及聊天室裡最近的對話歷史。

**Path**: Command（context 組裝是產生 AI 回覆這個寫入動作的一部分，不是獨立查詢）

#### Scenario: AI 回覆時能參考筆記內容與對話歷史
- **WHEN** AI 被 `@AI` 觸發產生回覆
- **THEN** 回覆內容的產生依據包含這篇筆記目前的內容，以及聊天室裡最近的對話歷史

### Requirement: 設定的 AI provider 無法回應時，系統自動改用備援 provider
當目前設定的 AI provider 無法處理請求時，系統 SHALL 依序嘗試下一個設定的備援 provider，直到有一個成功回應或全部嘗試完畢。

**Path**: Command

#### Scenario: 主要 provider 無法回應時改用備援
- **WHEN** 目前設定的 AI provider 無法處理請求
- **THEN** 系統改用下一個設定的備援 provider 嘗試產生回覆

### Requirement: 所有 AI provider 都失敗時，系統明確告知使用者
當所有設定的 AI provider 都無法產生回覆時，系統 SHALL 在聊天室中回覆一則訊息，明確告知使用者 AI 目前無法回應。

**Path**: Command

#### Scenario: 所有 provider 都無法回應
- **WHEN** 所有設定的 AI provider 都無法產生回覆
- **THEN** 系統在聊天室中回覆一則訊息，明確告知使用者 AI 目前無法回應
