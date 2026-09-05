## 1. Domain

- [ ] 1.1 在 `CoNotes.Domain` 定義 `LicenseCode` Aggregate Root（`Code`、`PlanTier`、兌換狀態、`RedeemedByAppUserId` 可為 null、`PayPalSubscriptionId`、`CreatedAt`、`RedeemedAt`），封裝「未兌換才能兌換」的不變條件；產生時發出 `LicenseCodeIssued`，兌換成功時發出 `LicenseCodeRedeemed`
- [ ] 1.2 單元測試（`GivenXXX_WhenXXX_ThenXXX`）：`GivenUnusedCode_WhenRedeemed_ThenMarkedRedeemedAndRaisesLicenseCodeRedeemed`
- [ ] 1.3 單元測試：`GivenAlreadyRedeemedCode_WhenRedeemAttempted_ThenRejectedAndNoEventRaised`
- [ ] 1.4 在既有的 `AppUser` Aggregate 新增 `PlanTier` 欄位與撤銷方法，封裝「撤銷後設回 Free 並發出 `AppUserPlanTierRevoked`」；新增接受 `LicenseCodeRedeemed` 事件、更新 `PlanTier` 的方法
- [ ] 1.5 單元測試：`GivenAppUserWithProTier_WhenRevoked_ThenPlanTierSetToFreeAndEventRaised`
- [ ] 1.6 單元測試：`GivenLicenseCodeRedeemedEvent_WhenAppliedToAppUser_ThenPlanTierMatchesEventPlanTier`

## 2. Application（Command / Query）

- [ ] 2.1 實作 `IssueLicenseCodeCommand` + Handler（由 PayPal webhook 觸發），單元測試（mock `ILicenseCodeRepository`）：`GivenFirstActivationWebhookEvent_WhenHandled_ThenLicenseCodeIssuedForCorrectPlanTier`
- [ ] 2.2 實作 `RedeemLicenseCodeCommand` + Handler，單元測試：`GivenValidCode_WhenRedeemCommandHandled_ThenLicenseCodeMarkedRedeemed`、`GivenAlreadyRedeemedCode_WhenRedeemCommandHandled_ThenRejected`
- [ ] 2.3 實作 `LicenseCodeRedeemed` 的跨 Context Event Handler（Identity context 訂閱，更新 `AppUser.PlanTier`），單元測試：`GivenLicenseCodeRedeemedEvent_WhenHandled_ThenAppUserPlanTierUpdated`
- [ ] 2.4 單元測試（關鍵不變條件）：`GivenRedeemedCode_WhenCorrespondingPayPalSubscriptionLaterCancelledOrPaymentFails_ThenAppUserPlanTierRemainsUnchanged`（驗證：因為系統只在兌換當下分派一次 Command，PayPal 後續狀態變化不會分派任何 Command，`PlanTier` 自然不受影響）
- [ ] 2.5 實作 `RevokeAppUserPlanTierCommand` + Handler（僅管理者可呼叫），單元測試：`GivenAdminRevokesUser_WhenCommandHandled_ThenPlanTierSetToFree`、`GivenNonAdminAttemptsRevoke_WhenCommandHandled_ThenRejected`
- [ ] 2.6 在既有 `GenerateShareLinkCommandHandler`（`collab-editing`）前置條件加上查詢 `AppUser.PlanTier` 的檢查，單元測試：`GivenOwnerPlanTierFree_WhenGenerateShareLinkCommandHandled_ThenRejected`、`GivenOwnerPlanTierProOrAbove_WhenGenerateShareLinkCommandHandled_ThenSucceeds`
- [ ] 2.7 在既有 `SendChatMessageCommandHandler`（`ai-chat`）前置條件加上查詢 `AppUser.PlanTier` 的檢查，單元測試：`GivenOwnerPlanTierNotProMax_WhenSendChatMessageCommandHandled_ThenRejected`、`GivenOwnerPlanTierProMax_WhenSendChatMessageCommandHandled_ThenSucceeds`
- [ ] 2.8 單元測試（軟性降級）：`GivenOwnerPlanTierRevoked_WhenQueryingExistingCollaboratorsOrChatHistory_ThenExistingDataUnaffected`

## 3. Infrastructure

- [ ] 3.1 撰寫 migration 為 `AppUser` 新增 `PlanTier` 欄位（`Free`/`Pro`/`ProMax`，預設 `Free`），驗證 down 可正確移除
- [ ] 3.2 撰寫 migration 新增 `LicenseCode` table，驗證 down 可正確移除
- [ ] 3.3 實作 `ILicenseCodeRepository` 的 Dapper 版本，Testcontainers 整合測試：`GivenPersistedCode_WhenRedeemedConcurrentlyByTwoRequests_ThenOnlyOneSucceeds`
- [ ] 3.4 實作 PayPal webhook 簽章驗證與事件類型判斷，只把「訂閱首次啟用成功」事件轉換成 `IssueLicenseCodeCommand`，其餘事件類型直接忽略；整合測試：`GivenNonActivationWebhookEventType_WhenReceived_ThenIgnoredAndNoLicenseCodeIssued`
- [ ] 3.5 架構測試：驗證 `CoNotes.Domain` 不參考 `CoNotes.Infrastructure`／`CoNotes.Api`

## 4. Api

- [ ] 4.1 新增建立 PayPal 訂閱 checkout 的 endpoint（sandbox）
- [ ] 4.2 新增接收 PayPal webhook 的 endpoint
- [ ] 4.3 新增兌換 license code 的 endpoint，Functional Test：`GivenValidUnusedCode_WhenRedeemEndpointCalled_ThenPlanTierUpdatedInResponse`、`GivenAlreadyRedeemedCode_WhenRedeemEndpointCalled_ThenReturnsRejection`
- [ ] 4.4 新增管理者撤銷等級的 endpoint，Functional Test：`GivenAdminUser_WhenRevokeEndpointCalled_ThenPlanTierSetToFree`、`GivenNonAdminUser_WhenRevokeEndpointCalled_ThenReturns403`
- [ ] 4.5 Functional Test（端對端）：`GivenPayPalActivationWebhook_WhenFollowedByRedeem_ThenOwnerCanGenerateShareLinkAndUseAiChatPerTier`

## 5. 前端

- [ ] 5.1 新增訂閱方案選擇畫面，串接 PayPal checkout 建立 endpoint
- [ ] 5.2 新增輸入 license code 兌換的畫面，驗證兌換成功/失敗（已使用過）兩種情況都有清楚的提示
- [ ] 5.3 在分享連結、聊天室相關 UI，針對等級不足的情況顯示清楚的提示（而非讓功能默默失效）

## 6. 端對端驗證

- [ ] 6.1 逐一驗證 `specs/subscription-billing/spec.md` 的五個 Requirement 全數通過
- [ ] 6.2 逐一驗證 `specs/collab-editing/spec.md`、`specs/ai-chat/spec.md` 這次修改的部分全數通過
- [ ] 6.3 完整跑一次流程：PayPal sandbox 訂閱 Pro → webhook 觸發產生 code → 兌換 → 產生分享連結成功；再訂閱 ProMax → 兌換 → 聊天室可用
