## 1. Domain

> PayPal 這裡只用一次性付款（Orders API），不是原本規劃的 Subscriptions API 定期訂閱——跟你確認過的決定，模型更單純：`LicenseCode` 上記的是 `PayPalOrderId`（單筆訂單），不是訂閱 id；不追蹤任何「訂閱」生命週期。`PlanTier` enum（`Free`/`Pro`/`ProMax`）放在 `CoNotes.Domain.AppUsers`（`AppUser` 是這份狀態的擁有者），`LicenseCode`（Billing context）跨 context 直接參照這個 enum——enum 是純值型別，不像 Aggregate 參照需要走 ID，這裡沿用既有的「共用詞彙」慣例。

- [x] 1.1 在 `CoNotes.Domain` 定義 `LicenseCode` Aggregate Root（`Code`、`PlanTier`、兌換狀態、`RedeemedByAppUserId` 可為 null、`PayPalOrderId`、`CreatedAt`、`RedeemedAt`），封裝「未兌換才能兌換」的不變條件；產生時發出 `LicenseCodeIssued`，兌換成功時發出 `LicenseCodeRedeemed`
- [x] 1.2 單元測試（`GivenXXX_WhenXXX_ThenXXX`）：`GivenUnusedCode_WhenRedeemed_ThenMarkedRedeemedAndRaisesLicenseCodeRedeemed`
- [x] 1.3 單元測試：`GivenAlreadyRedeemedCode_WhenRedeemAttempted_ThenRejectedAndNoEventRaised`
- [x] 1.4 在既有的 `AppUser` Aggregate 新增 `PlanTier` 欄位與撤銷方法，封裝「撤銷後設回 Free 並發出 `AppUserPlanTierRevoked`」；新增接受 `LicenseCodeRedeemed` 事件、更新 `PlanTier` 的方法（分別是 `RevokePlanTier()` 與 `ApplyRedeemedPlanTier(planTier)`；後者純粹是狀態轉換，不另外發事件——兌換本身已經由 `LicenseCodeRedeemed` 表達過)
- [x] 1.5 單元測試：`GivenAppUserWithProTier_WhenRevoked_ThenPlanTierSetToFreeAndEventRaised`
- [x] 1.6 單元測試：`GivenLicenseCodeRedeemedEvent_WhenAppliedToAppUser_ThenPlanTierMatchesEventPlanTier`

## 2. Application（Command / Query）

> **額外發現的一個真的 bug**：實作 2.3 時發現 `UnitOfWork.CommitAsync` 原本在 transaction commit 之後、但在 `appDbContext.Transaction` 被清空(設為 null)之前就呼叫 `PublishDomainEvens`——這個專案裡目前所有既有的 Domain Event Handler 都沒有真的再寫一次 DB(`AiReplyRequestedDomainEventHandler` 只是丟進背景 queue), 所以這個問題從沒被踩到過。`LicenseCodeRedeemedDomainEventHandler` 是第一個會在事件處理時透過 Repository 寫 DB 的 handler, 若沿用舊的呼叫順序, Repository 傳進去的 `transaction: appDbContext.Transaction` 會是一個「已經 commit 過」的 transaction 物件, Npgsql 直接丟 `InvalidOperationException: Transaction is already completed`（已經實測重現：暫時把修正 revert 掉重跑 `GivenRedeemedCode_WhenAppUserReloadedFromDb_ThenPlanTierWasUpdatedByTheCrossContextEventHandler`，確認會炸出這個例外；修好後同一個測試通過）。修法：把 `PublishDomainEvens` 移到 `try/finally`(commit + 清空 Transaction + dispose)完全執行完之後才呼叫，讓事件處理者的寫入落在原交易之外、各自獨立 autocommit。這個修正影響所有「事件處理者需要寫 DB」的情境，不只是這次新加的 `LicenseCodeRedeemedDomainEventHandler`，理論上修好了一個在這之前就存在、只是還沒被逼出來的 bug。
>
> `RevokeAppUserPlanTierCommand` 的管理者身份檢查在 `IUserContext` 新增同步的 `IsAdmin()` 方法（讀取目前 request 的 `ClaimsPrincipal.IsInRole("admin")`）——這不是 Application 層該處理的業務規則, 而是框架層級的角色驗證, 但選擇讓 Command Handler 自己檢查(而不是只在 endpoint 用 `RequireRole` 擋)，方便單元測試直接驗證「非管理者被拒絕」這個行為。Keycloak 把 realm role 放在 `realm_access` claim 裡(一段 JSON, `{"roles":[...]}`)，不是 ASP.NET Core 角色驗證看的那種一個角色一個 claim 的格式——在 `AuthenticationConfiguration.OnTokenValidated` 裡新增 `AddRealmRoleClaims`，把它攤平成標準的 `ClaimTypes.Role` claim。`infra/k8s/keycloak/realm-export/conotes-realm.json` 新增 `admin` realm role 定義，供之後重建環境時自動建立；本機既有的 Keycloak 需要透過 Admin Console/API 手動補上這個 role 給測試帳號。
>
> `SendChatMessageCommandHandler`/`GenerateShareLinkCommandHandler` 都額外注入 `IAppUserRepository`，查詢 `note.OwnerAppUserId` 對應的 `AppUser.PlanTier`；`IAppUserRepository` 也因此新增 `FindByIdAsync`／`UpdateAsync`（先前只有 `FindByKeycloakSubAsync`／`AddAsync`）。所有既有會呼叫這兩個 Command 的測試(`NoteCollaborationTests`／`ChatMessageTests`／`NoteCollabEndpointTests`／`NoteCollabHubReplicaTests`)都補上「先把擁有者的 `PlanTier` 設成 Pro/ProMax」的前置步驟；新增一個測試專用的 `POST /api/test/app-user/plan-tier` endpoint(跳過真的付款/兌換流程, 直接把目前使用者的等級設成指定值), 給 `NoteCollabEndpointTests`/`NoteCollabHubReplicaTests` 這類走真實 HTTP 的測試使用。

- [x] 2.1 實作 `IssueLicenseCodeCommand` + Handler（由確認付款完成的 Api endpoint 觸發，見 Section 3/4 的 webhook→導回confirm 決定），單元測試（mock `ILicenseCodeRepository`）：`GivenOrderCompletedWebhookEvent_WhenHandled_ThenLicenseCodeIssuedForCorrectPlanTier`（測試名稱沿用原本的命名，Handler 本身不知道也不在乎呼叫者是 webhook 還是導回確認，都只是「給定 PlanTier 跟 PayPalOrderId，發一組 code」)
- [x] 2.2 實作 `RedeemLicenseCodeCommand` + Handler，單元測試：`GivenValidCode_WhenRedeemCommandHandled_ThenLicenseCodeMarkedRedeemed`、`GivenAlreadyRedeemedCode_WhenRedeemCommandHandled_ThenRejected`（額外加了 `GivenConcurrentRedemptionLosesTheRace_WhenRedeemCommandHandled_ThenRejected`；真正的原子判斷在 Infrastructure 層的條件式 UPDATE，這裡驗證 Handler 對「持久化失敗」的反應)
- [x] 2.3 實作 `LicenseCodeRedeemed` 的跨 Context Event Handler（Identity context 訂閱，更新 `AppUser.PlanTier`），單元測試：`GivenLicenseCodeRedeemedEvent_WhenHandled_ThenAppUserPlanTierUpdated`（見上方 `UnitOfWork` bug 說明；另外用 Testcontainers 整合測試 `GivenRedeemedCode_WhenAppUserReloadedFromDb_ThenPlanTierWasUpdatedByTheCrossContextEventHandler` 對真的 Postgres 驗證整條「Redeem → commit → 事件發布 → 寫回 AppUser」的流程真的成功)
- [x] 2.4 單元測試（關鍵不變條件）：`GivenRedeemedCode_WhenCorrespondingPayPalPaymentLaterRefundedOrDisputed_ThenAppUserPlanTierRemainsUnchanged`（驗證：因為系統只在兌換當下分派一次 Command，PayPal 後續狀態變化不會分派任何 Command，`PlanTier` 自然不受影響——`RedeemLicenseCodeCommandHandlerTests` 的三個測試合起來就是這個不變條件：成功兌換只發生一次事件、已兌換的 code 不會再觸發任何動作、併發下輸掉競態的請求也不會)
- [x] 2.5 實作 `RevokeAppUserPlanTierCommand` + Handler（僅系統管理者可呼叫，管理者身份透過 Keycloak realm role `admin` 判斷，見 design.md 決定 3），單元測試：`GivenAdminRevokesUser_WhenCommandHandled_ThenPlanTierSetToFree`、`GivenNonAdminAttemptsRevoke_WhenCommandHandled_ThenRejected`
- [x] 2.6 在既有 `GenerateShareLinkCommandHandler`（`collab-editing`）前置條件加上查詢 `AppUser.PlanTier` 的檢查，單元測試：`GivenOwnerPlanTierFree_WhenGenerateShareLinkCommandHandled_ThenRejected`、`GivenOwnerPlanTierProOrAbove_WhenGenerateShareLinkCommandHandled_ThenSucceeds`
- [x] 2.7 在既有 `SendChatMessageCommandHandler`（`ai-chat`）前置條件加上查詢 `AppUser.PlanTier` 的檢查，單元測試：`GivenOwnerPlanTierNotProMax_WhenSendChatMessageCommandHandled_ThenRejected`、`GivenOwnerPlanTierProMax_WhenSendChatMessageCommandHandled_ThenSucceeds`（`GetChatHistoryQueryHandler` 刻意不動——讀取歷史訊息不受等級影響，只有傳送新訊息被擋，呼應 design.md 決定 7 的「軟性降級」)
- [x] 2.8 單元測試（軟性降級）：`GivenOwnerPlanTierRevoked_WhenQueryingExistingCollaboratorsOrChatHistory_ThenExistingDataUnaffected`（改用 Testcontainers 整合測試，跟 2.6/2.7 的既有慣例一致：擁有者 ProMax → 建立筆記、共編者加入、留言 → 撤銷擁有者等級回 Free → 驗證共編者仍能讀取筆記與聊天歷史)

## 3. Infrastructure

> **不用 webhook**：原本規劃 PayPal 付款完成後由 webhook 通知後端，後來發現這台開發機沒有對外可達的網址，PayPal 伺服器無法真的把 webhook 送過來——跟你確認過，改成「使用者從 PayPal 核准頁導回後，前端呼叫我們自己的確認 endpoint，後端直接呼叫 PayPal 的 Capture API、檢查回傳狀態是否為 `COMPLETED`」，不需要對外可達的網址，也能在本機完整測試（見 design.md 決定 2）。3.4 因此從「webhook 簽章驗證」改成「PayPal API client」。

- [x] 3.1 撰寫 migration 為 `AppUser` 新增 `PlanTier` 欄位（`Free`/`Pro`/`ProMax`，預設 `Free`），驗證 down 可正確移除
- [x] 3.2 撰寫 migration 新增 `LicenseCode` table，驗證 down 可正確移除
- [x] 3.3 實作 `ILicenseCodeRepository` 的 Dapper 版本，Testcontainers 整合測試：`GivenPersistedCode_WhenRedeemedConcurrentlyByTwoRequests_ThenOnlyOneSucceeds`
- [x] 3.4 實作 `IPayPalClient`（OAuth2 client-credentials 換 access token、建立訂單、capture 訂單），呼叫真的 PayPal Sandbox REST API 驗證：建立訂單成功並取得可核准的網址（`PayPalClientTests`：`GivenValidCredentials_WhenCreatingOrder_ThenReturnsOrderIdAndApprovalUrl` 真的打 `api-m.sandbox.paypal.com` 建立訂單並取得 `approve` 連結；`GivenOrderThatHasNotBeenApprovedYet_WhenCapturing_ThenReturnsNotCompleted` 驗證還沒核准就 capture 會被 PayPal 拒絕（`422 ORDER_NOT_APPROVED`），`CaptureOrderAsync` 正確轉換成 `IsCompleted: false` 而不是拋例外；`PlanTier` 透過 `purchase_units[].custom_id` 帶去、從 capture 回應的 `purchase_units[].payments.captures[].custom_id` 讀回)
- [x] 3.5 架構測試：驗證 `CoNotes.Domain` 不參考 `CoNotes.Infrastructure`／`CoNotes.Api`（沿用既有的 `LayerDependencyTests`，新增的型別都在既有專案內，自動涵蓋)

## 4. Api

> 新增 `BillingGroupEndpoint`（`/api/v1/billing`，比照 `NoteGroupEndpoint` 的慣例）。`CreateOrder`/`ConfirmOrder` 直接注入 `IPayPalClient`（不透過 Command——建立/確認訂單本身不是任何 Aggregate 的操作，比照 `NoteCollabHub` 直接呼叫 `INoteEditHistoryStore` 的先例），`ConfirmOrder` 確認 capture 成功後才呼叫 `IssueLicenseCodeCommand`。撤銷等級的 endpoint 用 `.RequireAuthorization(policy => policy.RequireRole("admin"))`——這是框架層級的角色檢查，跟 `RevokeAppUserPlanTierCommandHandler` 自己的 `IsAdmin()` 檢查是兩層防護：框架層讓真的 HTTP 呼叫在指令都還沒分派前就被 403 擋下；Handler 層則保護任何繞過 HTTP、直接用 `ISender` 呼叫這個 Command 的情境（例如 `LicenseCodeRedemptionTests` 這類直接用 `ISender` 呼叫的整合測試)。順便讓 `PlanTier` 這類 enum 在 JSON 回應裡序列化成字串（例如 `"ProMax"`）而不是底層數字，前端可讀性/穩定性都比較好（`Program.cs` 全域設定 `JsonStringEnumConverter`)。

- [x] 4.1 新增建立 PayPal 訂單（Orders API）的 endpoint（sandbox），回傳使用者要導去核准的 PayPal 網址
- [x] 4.2 新增確認付款完成的 endpoint（使用者從 PayPal 核准頁導回後，前端呼叫這個 endpoint 帶著 PayPal Order Id；後端呼叫 Capture API，狀態為 `COMPLETED` 才觸發 `IssueLicenseCodeCommand`，否則回覆失敗、不產生 code）
- [x] 4.3 新增兌換 license code 的 endpoint，Functional Test：`GivenValidUnusedCode_WhenRedeemEndpointCalled_ThenPlanTierUpdatedInResponse`、`GivenAlreadyRedeemedCode_WhenRedeemEndpointCalled_ThenReturnsRejection`（測試前置狀態靠新增的 `POST /api/test/license-code`（跳過真的付款流程直接發一組 code），比照既有的 `POST /api/test/app-user/plan-tier`）
- [x] 4.4 新增管理者撤銷等級的 endpoint（`RequireAuthorization` 要求 `admin` realm role），Functional Test：`GivenAdminUser_WhenRevokeEndpointCalled_ThenPlanTierSetToFree`、`GivenNonAdminUser_WhenRevokeEndpointCalled_ThenReturns403`（`TestTokens.CreateToken` 新增 `extraClaims` 參數，讓測試能簽出帶 `ClaimTypes.Role: "admin"` 的 JWT)
- [x] 4.5 Functional Test（端對端）：`GivenPayPalOrderCreatedButNotApproved_WhenConfirmCalled_ThenRejectedAndNoLicenseCodeIssued`（用真的 PayPal Sandbox REST API 建立訂單、取得可核准的網址；還沒核准就呼叫 confirm，驗證正確回 400、不產生 code。核准這一步需要 PayPal sandbox buyer 帳號，見 6.0/6.3；兌換後產生分享連結/使用聊天室這段由 4.3 + 既有的 `NoteCollabEndpointTests`/`ChatMessageTests` 涵蓋)

## 5. 前端

> 新增 `features/billing/`：`BillingPlansComponent`（`/billing/plans`）、`BillingConfirmComponent`（`/billing/confirm`，讀取 PayPal 導回時帶的 `?token=` query param 當 order id）、`RedeemCodeComponent`（`/billing/redeem`）。`note-editor.component.ts` 的分享連結、`note-chat.component.ts` 的聊天室送出，都補上失敗時的錯誤訊息顯示(原本這兩個動作完全沒有處理 subscribe 的 error callback，會直接靜默失敗)。順便讓 `note-list` 加一個「訂閱方案」的連結，否則畫面上完全沒有入口。

- [x] 5.1 新增訂閱方案選擇畫面，呼叫建立訂單 endpoint 後導向 PayPal 核准網址（Playwright 實測：真的建立臨時 Keycloak 使用者、登入、點擊「購買 Pro」，確認瀏覽器真的被導向 `sandbox.paypal.com` 的核准頁面）
- [x] 5.2 新增使用者從 PayPal 核准頁導回後的確認畫面，呼叫確認付款完成 endpoint，顯示產生的 license code
- [x] 5.3 新增輸入 license code 兌換的畫面，驗證兌換成功/失敗（已使用過）兩種情況都有清楚的提示（Playwright 實測：用測試專用 endpoint 取得一組 code(因為核准這步需要 buyer 帳號，見 6.0)、在 `/billing/redeem` 輸入兌換，畫面正確顯示「兌換成功！你的訂閱等級已更新為 Pro。」)
- [x] 5.4 在分享連結、聊天室相關 UI，針對等級不足的情況顯示清楚的提示（而非讓功能默默失效）（Playwright 實測兩種情境：Free tier 使用者點「產生分享連結」，畫面顯示「訂閱等級不足」提示；兌換 Pro 之後同一顆按鈕改為成功；Pro(非 ProMax) tier 使用者在聊天室送訊息，畫面顯示「筆記擁有者的訂閱等級須為 ProMax」提示）

## 6. 端對端驗證

> 6.3 的「使用者在 PayPal 頁面核准付款」這一步需要一組 PayPal sandbox **buyer(測試買家)帳號**——這跟目前已經提供的 sandbox app 用戶端 id/密鑰(用來讓後端呼叫 PayPal API)是不同的東西，buyer 帳號是拿來登入 PayPal 核准頁面用的個人測試帳號，通常在 PayPal Developer Dashboard 的 sandbox 帳號清單裡就有預設的一組。拿到這組帳密後可以用 Playwright 完整跑過核准頁的登入與按下核准按鈕，跟 Keycloak 登入走的是同一套自動化方式。

- [ ] 6.0 取得一組 PayPal sandbox buyer 帳號（email/密碼），供 Playwright 自動化核准流程使用

- [ ] 6.1 逐一驗證 `specs/subscription-billing/spec.md` 的五個 Requirement 全數通過
- [ ] 6.2 逐一驗證 `specs/collab-editing/spec.md`、`specs/ai-chat/spec.md` 這次修改的部分全數通過
- [ ] 6.3 完整跑一次流程：PayPal sandbox 付款購買 Pro → 核准並導回確認 → 產生 code → 兌換 → 產生分享連結成功；再付款購買 ProMax → 兌換 → 聊天室可用（需要手動核准，見上方註記）
