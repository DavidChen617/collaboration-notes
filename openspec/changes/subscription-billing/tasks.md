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

- [x] 2.1 實作 `IssueLicenseCodeCommand` + Handler（由 PayPal 的 `PAYMENT.CAPTURE.COMPLETED` webhook 觸發，見 Section 3/4），單元測試（mock `ILicenseCodeRepository`）：`GivenOrderCompletedWebhookEvent_WhenHandled_ThenLicenseCodeIssuedForCorrectPlanTier`；額外加了 `GivenOrderAlreadyHasALicenseCode_WhenHandledAgain_ThenReturnsExistingCodeWithoutIssuingANewOne`——webhook 可能因為逾時被 PayPal 重送同一個事件, Handler 必須是 idempotent 的, 靠 `PayPalOrderId` 判斷「這筆訂單是不是已經發過 code」, 已經發過就直接回傳原本那組, 不要重複發
- [x] 2.2 實作 `RedeemLicenseCodeCommand` + Handler，單元測試：`GivenValidCode_WhenRedeemCommandHandled_ThenLicenseCodeMarkedRedeemed`、`GivenAlreadyRedeemedCode_WhenRedeemCommandHandled_ThenRejected`（額外加了 `GivenConcurrentRedemptionLosesTheRace_WhenRedeemCommandHandled_ThenRejected`；真正的原子判斷在 Infrastructure 層的條件式 UPDATE，這裡驗證 Handler 對「持久化失敗」的反應)
- [x] 2.3 實作 `LicenseCodeRedeemed` 的跨 Context Event Handler（Identity context 訂閱，更新 `AppUser.PlanTier`），單元測試：`GivenLicenseCodeRedeemedEvent_WhenHandled_ThenAppUserPlanTierUpdated`（見上方 `UnitOfWork` bug 說明；另外用 Testcontainers 整合測試 `GivenRedeemedCode_WhenAppUserReloadedFromDb_ThenPlanTierWasUpdatedByTheCrossContextEventHandler` 對真的 Postgres 驗證整條「Redeem → commit → 事件發布 → 寫回 AppUser」的流程真的成功)
- [x] 2.4 單元測試（關鍵不變條件）：`GivenRedeemedCode_WhenCorrespondingPayPalPaymentLaterRefundedOrDisputed_ThenAppUserPlanTierRemainsUnchanged`（驗證：因為系統只在兌換當下分派一次 Command，PayPal 後續狀態變化不會分派任何 Command，`PlanTier` 自然不受影響——`RedeemLicenseCodeCommandHandlerTests` 的三個測試合起來就是這個不變條件：成功兌換只發生一次事件、已兌換的 code 不會再觸發任何動作、併發下輸掉競態的請求也不會)
- [x] 2.5 實作 `RevokeAppUserPlanTierCommand` + Handler（僅系統管理者可呼叫，管理者身份透過 Keycloak realm role `admin` 判斷，見 design.md 決定 3），單元測試：`GivenAdminRevokesUser_WhenCommandHandled_ThenPlanTierSetToFree`、`GivenNonAdminAttemptsRevoke_WhenCommandHandled_ThenRejected`
- [x] 2.6 在既有 `GenerateShareLinkCommandHandler`（`collab-editing`）前置條件加上查詢 `AppUser.PlanTier` 的檢查，單元測試：`GivenOwnerPlanTierFree_WhenGenerateShareLinkCommandHandled_ThenRejected`、`GivenOwnerPlanTierProOrAbove_WhenGenerateShareLinkCommandHandled_ThenSucceeds`
- [x] 2.7 在既有 `SendChatMessageCommandHandler`（`ai-chat`）前置條件加上查詢 `AppUser.PlanTier` 的檢查，單元測試：`GivenOwnerPlanTierNotProMax_WhenSendChatMessageCommandHandled_ThenRejected`、`GivenOwnerPlanTierProMax_WhenSendChatMessageCommandHandled_ThenSucceeds`（`GetChatHistoryQueryHandler` 刻意不動——讀取歷史訊息不受等級影響，只有傳送新訊息被擋，呼應 design.md 決定 7 的「軟性降級」)
- [x] 2.8 單元測試（軟性降級）：`GivenOwnerPlanTierRevoked_WhenQueryingExistingCollaboratorsOrChatHistory_ThenExistingDataUnaffected`（改用 Testcontainers 整合測試，跟 2.6/2.7 的既有慣例一致：擁有者 ProMax → 建立筆記、共編者加入、留言 → 撤銷擁有者等級回 Free → 驗證共編者仍能讀取筆記與聊天歷史)

## 3. Infrastructure

> **webhook 機制經過兩次調整**：一開始規劃 webhook，後來發現這台開發機沒有對外可達的網址、PayPal 無法把 webhook 送到這裡，改成導回後直接呼叫 Capture API 確認；後來用 `ngrok http 5094` 架了一個公開隧道，改回 webhook 驅動（見 design.md 決定 2）。`IPayPalClient` 新增 `TryVerifyCaptureCompletedEventAsync`：呼叫 PayPal 的 `verify-webhook-signature` API 驗證簽章、確認事件類型是 `PAYMENT.CAPTURE.COMPLETED`、從 `resource.custom_id`／`resource.supplementary_data.related_ids.order_id` 解析出 `PlanTier`／`PayPalOrderId`；任何一步失敗都回傳 `null`，呼叫端一律回 200 給 PayPal（避免它重送)。`PayPal:WebhookId` 沒設定時直接拋例外，不是靜默回傳 null——設定缺失跟「簽章驗證失敗」是不同情況，前者是我們自己的錯，不該被誤判成攻擊/無效請求。

- [x] 3.1 撰寫 migration 為 `AppUser` 新增 `PlanTier` 欄位（`Free`/`Pro`/`ProMax`，預設 `Free`），驗證 down 可正確移除
- [x] 3.2 撰寫 migration 新增 `LicenseCode` table，驗證 down 可正確移除
- [x] 3.3 實作 `ILicenseCodeRepository` 的 Dapper 版本，Testcontainers 整合測試：`GivenPersistedCode_WhenRedeemedConcurrentlyByTwoRequests_ThenOnlyOneSucceeds`（額外新增 `GetByPayPalOrderIdAsync`，供 idempotent 發 code 與前端輪詢 code 用）
- [x] 3.4 實作 `IPayPalClient`（OAuth2 client-credentials 換 access token、建立訂單、capture 訂單、驗證 webhook 簽章），呼叫真的 PayPal Sandbox REST API 驗證：
  - `GivenValidCredentials_WhenCreatingOrder_ThenReturnsOrderIdAndApprovalUrl` 真的打 `api-m.sandbox.paypal.com` 建立訂單並取得 `approve` 連結
  - `GivenOrderThatHasNotBeenApprovedYet_WhenCapturing_ThenReturnsNotCompleted` 驗證還沒核准就 capture 會被 PayPal 拒絕（`422 ORDER_NOT_APPROVED`），`CaptureOrderAsync` 正確轉換成 `IsCompleted: false` 而不是拋例外
  - `GivenForgedWebhookTransmissionData_WhenVerifying_ThenReturnsNull` 用真的向 PayPal 註冊過的 webhook id、送一組捏造的 transmission 簽章資訊給 `verify-webhook-signature` API，確認 PayPal 真的回報驗證失敗
  - **真的用 ngrok 跑過一次端對端的 webhook 送達**：`ngrok http 5094` 拿到公開網址，透過 PayPal REST API（`POST /v1/notifications/webhooks`）註冊一個指向這個網址、訂閱 `PAYMENT.CAPTURE.COMPLETED` 的 webhook；再用 PayPal 的 webhook 模擬器 API（`POST /v1/notifications/simulate-event`）觸發一次真正由 PayPal 簽章的事件送到本機——ngrok 的 inspector 與應用程式 log 確認請求真的送達、`TryVerifyCaptureCompletedEventAsync` 的簽章驗證真的通過；因為模擬器帶的是固定的假資料（`custom_id` 不是有效的 `PlanTier`），正確地判斷解析失敗、沒有產生任何 `license_codes` 資料列（直接查 Postgres 確認），行為符合預期
- [x] 3.5 架構測試：驗證 `CoNotes.Domain` 不參考 `CoNotes.Infrastructure`／`CoNotes.Api`（沿用既有的 `LayerDependencyTests`，新增的型別都在既有專案內，自動涵蓋)

## 4. Api

> 新增 `BillingGroupEndpoint`（`/api/v1/billing`，比照 `NoteGroupEndpoint` 的慣例）。`CreateOrder`/`ConfirmOrder` 直接注入 `IPayPalClient`（不透過 Command——建立訂單、觸發 capture 本身不是任何 Aggregate 的操作，比照 `NoteCollabHub` 直接呼叫 `INoteEditHistoryStore` 的先例）。**`ConfirmOrder` 現在只負責觸發 capture**（回 202 Accepted 或 400），不直接發 code——真的發 code 是 `PayPalWebhookEndpoint`（`POST /billing/webhook`，`AllowAnonymous`，PayPal 不會帶我們的 JWT）驗證簽章通過後才觸發 `IssueLicenseCodeCommand`。新增 `GetLicenseCodeForOrderEndpoint`（`GET /billing/orders/{orderId}/license-code`）讓前端在導回確認頁時輪詢——webhook 送達是非同步的，使用者導回的當下不保證 code 已經產生。撤銷等級的 endpoint 用 `.RequireAuthorization(policy => policy.RequireRole("admin"))`——這是框架層級的角色檢查，跟 `RevokeAppUserPlanTierCommandHandler` 自己的 `IsAdmin()` 檢查是兩層防護：框架層讓真的 HTTP 呼叫在指令都還沒分派前就被 403 擋下；Handler 層則保護任何繞過 HTTP、直接用 `ISender` 呼叫這個 Command 的情境（例如 `LicenseCodeRedemptionTests` 這類直接用 `ISender` 呼叫的整合測試)。順便讓 `PlanTier` 這類 enum 在 JSON 回應裡序列化成字串（例如 `"ProMax"`）而不是底層數字，前端可讀性/穩定性都比較好（`Program.cs` 全域設定 `JsonStringEnumConverter`)。

- [x] 4.1 新增建立 PayPal 訂單（Orders API）的 endpoint（sandbox），回傳使用者要導去核准的 PayPal 網址
- [x] 4.2 新增接收 PayPal webhook 的 endpoint（`AllowAnonymous`；驗證簽章、確認事件類型是 `PAYMENT.CAPTURE.COMPLETED` 才觸發 `IssueLicenseCodeCommand`，其餘一律回 200 但不做任何事）；另外新增觸發 capture 的 endpoint（使用者從 PayPal 核准頁導回後呼叫，帶著 PayPal Order Id）與查詢 order 對應 code 的輪詢 endpoint
- [x] 4.3 新增兌換 license code 的 endpoint，Functional Test：`GivenValidUnusedCode_WhenRedeemEndpointCalled_ThenPlanTierUpdatedInResponse`、`GivenAlreadyRedeemedCode_WhenRedeemEndpointCalled_ThenReturnsRejection`（測試前置狀態靠新增的 `POST /api/test/license-code`（跳過真的付款流程直接發一組 code），比照既有的 `POST /api/test/app-user/plan-tier`）
- [x] 4.4 新增管理者撤銷等級的 endpoint（`RequireAuthorization` 要求 `admin` realm role），Functional Test：`GivenAdminUser_WhenRevokeEndpointCalled_ThenPlanTierSetToFree`、`GivenNonAdminUser_WhenRevokeEndpointCalled_ThenReturns403`（`TestTokens.CreateToken` 新增 `extraClaims` 參數，讓測試能簽出帶 `ClaimTypes.Role: "admin"` 的 JWT)
- [x] 4.5 Functional Test（端對端）：
  - `GivenPayPalOrderCreatedButNotApproved_WhenConfirmCalled_ThenRejectedAndNoLicenseCodeIssued`：用真的 PayPal Sandbox REST API 建立訂單、取得可核准的網址；還沒核准就呼叫 confirm，驗證正確回 400、輪詢 license-code 的 endpoint 也正確回 404
  - `GivenForgedWebhookRequest_WhenPostedAnonymously_ThenReturns200AndDoesNothing`：不帶任何 Authorization header（`AllowAnonymous` 真的生效）送一個假簽章的 webhook request，驗證回 200 且沒有產生 code
  - 核准這一步需要 PayPal sandbox buyer 帳號才能讓整條「建立訂單 → 核准 → capture → webhook → 發 code」全部自動化跑完，見 6.0/6.3；webhook 本身的送達與簽章驗證已經用 ngrok + PayPal 的 webhook 模擬器真的驗證過(見 3.4)；兌換後產生分享連結/使用聊天室這段由 4.3 + 既有的 `NoteCollabEndpointTests`/`ChatMessageTests` 涵蓋

## 5. 前端

> 新增 `features/billing/`：`BillingPlansComponent`（`/billing/plans`）、`BillingConfirmComponent`（`/billing/confirm`，讀取 PayPal 導回時帶的 `?token=` query param 當 order id）、`RedeemCodeComponent`（`/billing/redeem`）。`note-editor.component.ts` 的分享連結、`note-chat.component.ts` 的聊天室送出，都補上失敗時的錯誤訊息顯示(原本這兩個動作完全沒有處理 subscribe 的 error callback，會直接靜默失敗)。順便讓 `note-list` 加一個「訂閱方案」的連結，否則畫面上完全沒有入口。

- [x] 5.1 新增訂閱方案選擇畫面，呼叫建立訂單 endpoint 後導向 PayPal 核准網址（Playwright 實測：真的建立臨時 Keycloak 使用者、登入、點擊「購買 Pro」，確認瀏覽器真的被導向 `sandbox.paypal.com` 的核准頁面）
- [x] 5.2 新增使用者從 PayPal 核准頁導回後的確認畫面，呼叫觸發 capture 的 endpoint 後開始輪詢 code（webhook 送達是非同步的，capture 呼叫回應本身不代表 code 已經產生；每 1 秒查一次、最多 20 次，逾時顯示清楚的提示而不是無限轉圈）
- [x] 5.3 新增輸入 license code 兌換的畫面，驗證兌換成功/失敗（已使用過）兩種情況都有清楚的提示（Playwright 實測：用測試專用 endpoint 取得一組 code(因為核准這步需要 buyer 帳號，見 6.0)、在 `/billing/redeem` 輸入兌換，畫面正確顯示「兌換成功！你的訂閱等級已更新為 Pro。」)
- [x] 5.4 在分享連結、聊天室相關 UI，針對等級不足的情況顯示清楚的提示（而非讓功能默默失效）（Playwright 實測兩種情境：Free tier 使用者點「產生分享連結」，畫面顯示「訂閱等級不足」提示；兌換 Pro 之後同一顆按鈕改為成功；Pro(非 ProMax) tier 使用者在聊天室送訊息，畫面顯示「筆記擁有者的訂閱等級須為 ProMax」提示）

## 6. 端對端驗證

> 6.3 剩下的唯一缺口是「使用者在 PayPal 頁面核准付款」這一步，需要一組 PayPal sandbox **buyer(測試買家)帳號**——這跟目前已經提供的 sandbox app 用戶端 id/密鑰(用來讓後端呼叫 PayPal API)是不同的東西，buyer 帳號是拿來登入 PayPal 核准頁面用的個人測試帳號，通常在 PayPal Developer Dashboard 的 sandbox 帳號清單裡就有預設的一組。拿到這組帳密後可以用 Playwright 完整跑過核准頁的登入與按下核准按鈕，跟 Keycloak 登入走的是同一套自動化方式。webhook 本身的送達、簽章驗證、事件處理這幾層已經用 ngrok 對真的 PayPal 完整驗證過(見 3.4)，剩下沒驗證到的只有「一筆真的由使用者核准、金額正確扣款的訂單，走到這條 webhook 路徑上，最後真的產生 code」這個特定情境。

- [ ] 6.0 取得一組 PayPal sandbox buyer 帳號（email/密碼），供 Playwright 自動化核准流程使用

- [x] 6.1 逐一驗證 `specs/subscription-billing/spec.md` 的五個 Requirement 全數通過（「透過 PayPal 付款取得 code」：`PayPalClientTests`(真的打 sandbox API 建立訂單/capture/驗證 webhook 簽章) + `IssueLicenseCodeCommandHandlerTests`(含 idempotency) + `BillingEndpointTests`(未核准就 confirm 正確拒絕、webhook endpoint 正確驗證簽章)——真的用 ngrok + PayPal webhook 模擬器對本機送過一次真正簽過章的事件，確認送達與簽章驗證都成功；只有「核准後成功 capture、custom_id 是有效 PlanTier」這個特定組合需要 buyer 帳號才能走到，見 6.0；「兌換有效 code」：`RedeemLicenseCodeCommandHandlerTests` + `BillingEndpointTests` + Playwright 實測；「已兌換的 code 不能再兌換」：`RedeemLicenseCodeCommandHandlerTests`(`GivenAlreadyRedeemedCode...`) + `BillingEndpointTests`(`GivenAlreadyRedeemedCode...`)；「已兌換等級不隨 PayPal 後續狀態變動」：`RedeemLicenseCodeCommandHandlerTests` 三個測試合起來證明的不變條件(見 2.4)；「管理者手動撤銷」：`RevokeAppUserPlanTierCommandHandlerTests` + `BillingEndpointTests` + `LicenseCodeRedemptionTests`(真的對 Postgres 驗證撤銷後重新讀回是 Free)。完整 `CoNotes.slnx` 135/135 通過)
- [x] 6.2 逐一驗證 `specs/collab-editing/spec.md`、`specs/ai-chat/spec.md` 這次修改的部分全數通過（分享連結需要 Pro 以上：`GenerateShareLinkCommandHandlerTests` 的 Free/Pro/ProMax 三種情況 + Playwright 實測(Free 被拒、兌換 Pro 後成功)；聊天室需要 ProMax：`SendChatMessageCommandHandlerTests` 的 Free/Pro/ProMax 三種情況 + Playwright 實測(Pro 被拒並顯示提示)；共編者/聊天室參與者不需要自己也符合等級：沿用既有的 owner-only 檢查、共編者本來就不會被查 `PlanTier`，`NoteCollaborationTests`/`ChatMessageTests` 的既有共編者情境未受影響；軟性降級(撤銷後既有共編者/聊天記錄不受影響)：`GivenOwnerPlanTierRevoked_WhenQueryingExistingCollaboratorsOrChatHistory_ThenExistingDataUnaffected` 整合測試)
- [ ] 6.3 完整跑一次流程：PayPal sandbox 付款購買 Pro → 核准並導回確認 → webhook 送達並驗證簽章 → 產生 code → 輪詢拿到 code → 兌換 → 產生分享連結成功；再付款購買 ProMax → 兌換 → 聊天室可用（需要 buyer 帳號才能自動化核准這一步，見上方說明與 6.0；webhook 這條路徑本身已經用 ngrok 真的驗證過送達與簽章，剩下沒打通的只有「真的核准+真的扣款」這個環節）
