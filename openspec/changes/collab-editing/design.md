## Context

見 proposal.md 的 Why。建立在 `notes-crud`（`notes` capability）與 `note-linking`（Tiptap 編輯器、`[[` 連結）之上。基礎設施面，`setup-infra-and-auth` 已經建好 SignalR 需要的 Redis backplane。訂閱等級（Pro 才能用共編）本身不在這個 change 處理，這裡假設功能對所有使用者開放，等 `subscription-billing` change 再疊加分級限制。

## Goals / Non-Goals

**Goals:**
- 擁有者可以產生分享連結、撤銷連結，也可以直接移除某位共編者。
- 共編者可以即時看到彼此對筆記內容的編輯，不會互相覆蓋或衝突。
- 筆記保留完整編輯歷史，且歷史紀錄不會無限增長拖垮效能。
- 既有的 `notes` 存取規則正確放寬給共編者，同時不影響擁有者專屬的操作（刪除、管理共編者名單）。

**Non-Goals:**
- 訂閱等級限制（例如「只有 Pro 才能產生分享連結」）——留給 `subscription-billing` change 疊加。
- 共編者管理共編者名單或分享連結（例如共編者自己撤銷/重新產生連結）——僅擁有者能管理。
- 未登入的匿名訪問——開啟分享連結仍然需要透過 Keycloak 登入。

## Aggregate 邊界

這個 change 不新增 Aggregate，而是延續 `notes-crud` 建立的 `Note` Aggregate，擴充其不變條件：分享連結（`ShareToken`）與共編者名單的加入/移除，都是 `Note` 自身要保護的狀態——「只有擁有者能產生/撤銷連結、管理共編者名單」這條規則，必須由 `Note` Aggregate Root 在每次操作時檢查，不能讓外部直接寫入 `NoteCollaborator`/`ShareToken` 而繞過這個規則。曾考慮把 `NoteCollaborator` 獨立成一個 Aggregate——否決，因為它沒有自己獨立的一致性邊界，永遠是 `Note` 底下依附的狀態，脫離 `Note` 談「誰是共編者」沒有意義。即時協作編輯的 Yjs update log/snapshot 純粹是內容持久化的技術手段，不承載任何業務不變條件，歸類為 Infrastructure 層的儲存機制，不是 Domain Aggregate。

## Domain Events

- **`NoteShareLinkGenerated`**（`NoteId`、`ShareToken`、`OccurredAt`）：擁有者產生分享連結時發出。
- **`NoteShareLinkRevoked`**（`NoteId`、`OccurredAt`）：擁有者撤銷分享連結時發出。
- **`NoteCollaboratorJoined`**（`NoteId`、`AppUserId`、`OccurredAt`）：使用者透過有效的分享連結加入共編者名單時發出。
- **`NoteCollaboratorRemoved`**（`NoteId`、`AppUserId`、`OccurredAt`）：擁有者移除某位共編者時發出。

目前沒有其他 Aggregate/Context 監聽這四個事件，但先定義出來，供之後（例如通知、稽核記錄）掛勾子用。即時編輯過程中逐筆的 Yjs update 同步不建模成 Domain Event——那是高頻率的技術層同步動作，不是業務層面有意義的狀態轉變。

## Sequence：產生分享連結、加入共編

```mermaid
sequenceDiagram
    participant Owner as 筆記擁有者
    participant Api as CoNotes.Api
    participant GenCmd as GenerateShareLinkCommandHandler
    participant Note as Note Aggregate
    participant Repo as Note Repository
    participant Guest as 另一位已登入使用者
    participant JoinCmd as JoinNoteViaShareLinkCommandHandler

    Owner->>Api: 產生分享連結
    Api->>GenCmd: GenerateShareLinkCommand(NoteId)
    GenCmd->>Note: 驗證呼叫者為擁有者、產生 ShareToken
    Note-->>GenCmd: 發出 NoteShareLinkGenerated
    GenCmd->>Repo: 寫入 ShareToken
    Repo-->>Owner: 回傳分享連結

    Guest->>Api: 開啟分享連結
    Api->>JoinCmd: JoinNoteViaShareLinkCommand(ShareToken, AppUserId)
    JoinCmd->>Note: 驗證 ShareToken 有效
    Note-->>JoinCmd: 加入 AppUserId 至共編者名單、發出 NoteCollaboratorJoined
    JoinCmd->>Repo: 寫入 NoteCollaborator
    Repo-->>Guest: 加入成功
```

## Decisions

**1. 用 Yjs 做 CRDT 合併，伺服器（SignalR Hub）只負責轉發，不理解合併邏輯。**
Yjs 的協議設計就是「client 端做所有合併邏輯，server 只要存 + 轉發 binary update」。這代表 SignalR Hub 收到一個 client 的 update，直接 broadcast 給同一篇筆記的其他 client，不需要在後端實作任何 OT/CRDT 演算法。

**2. SignalR Hub 用 NoteId 當 Group，只有擁有者跟該筆記的共編者可以加入該 Group。**
加入 Group 前先做跟 REST API 一致的存取檢查（擁有者或共編者），避免無關使用者透過 SignalR 直接繞過權限拿到即時編輯內容。

**3. 保留完整編輯歷史，用「append-only 更新紀錄 + 定期快照壓縮」儲存。**
每個 Yjs update 先 append 進更新紀錄 table；當某篇筆記累積的更新筆數超過門檻，就把目前狀態合併成一個快照，寫進快照 table，並清掉已經合併進快照的舊更新紀錄。要重建某個時間點的內容，就是「最近一個快照 + 之後的更新紀錄」疊加回放。這樣「完整歷史」還在（可以往回播放），但不會讓更新紀錄無限增長。

**4. 壓縮觸發時機：存檔時檢查更新紀錄筆數，超過門檻就順便壓縮，不另外開背景排程。**
避免多加一個定時任務的基礎設施，用「存檔的當下順便檢查」這個最簡單的觸發點。

**5. 用分享連結取代「邀請」機制：每篇筆記可以產生一個帶有唯一、不可猜測 token 的分享連結；已登入的使用者開啟連結後，系統自動把該使用者加入這篇筆記的共編者名單。**
比起先前設計的「擁有者輸入對方 email 查詢既有 `AppUser`」，這樣不需要擁有者事先知道對方的身份，也不會遇到「對方從未登入過、查不到 `AppUser`」這個尷尬情境——任何人只要能登入，就能透過連結加入。同一條連結可以被多個不同的人重複使用（類似 Google Docs 的共用連結），不是一次性邀請碼。

**6. 擁有者可以撤銷目前的分享連結，讓舊連結失效；系統可以另外產生一條新的有效連結。撤銷連結不影響已經加入的共編者。**
「連結」與「共編者名單」是兩個獨立概念——撤銷連結只是關掉「新人能不能繼續透過這條連結加入」的開關，不會把已經在名單裡的人移除；移除既有共編者要走另一個獨立的操作（見 Goals）。

**7. 存取檢查沿用 `notes-crud` 定案的做法：純 SQL 條件（`OwnerAppUserId = @user OR EXISTS (SELECT 1 FROM NoteCollaborator WHERE NoteId = @note AND AppUserId = @user)`），不引入授權函式庫。**
共編者只是多一種「誰能存取」的條件，複雜度增加有限，還不到需要 Casbin/OpenFGA 這類授權引擎的程度（這個決策先前已經討論過，這裡延續同樣的判斷）。

## Risks / Trade-offs

- **[Risk]** SignalR 只做轉發、不驗證 Yjs update 內容本身合不合法——理論上一個惡意的共編者可以送出破壞內容一致性的資料。→ **Mitigation**：共編者是透過分享連結主動加入的人，不是完全不特定的大眾，這個信任邊界在玩具規模下可以接受；之後真的有需要可以再加內容完整性檢查。
- **[Risk]** 分享連結是多人可重複使用的，比起一次性邀請碼，連結一旦外流（例如被轉貼到公開的地方），任何看到連結的人只要登入就能加入共編。→ **Mitigation**：擁有者可以隨時撤銷連結、產生新的；文件裡（或 UI 上）明確提醒使用者這條連結等同於「知道連結就能編輯」，不要任意分享。
- **[Risk]** 壓縮門檻設太高，更新紀錄還是會累積不少；設太低，存檔時常常要做額外的壓縮運算。→ **Mitigation**：門檻值留到實作時依實際測試調整，不影響這裡的整體設計。
