**Bounded Context**: Notes｜**Aggregate**: `Note`

## Why

Pro 訂閱等級的核心賣點是「共享編輯」——讓多個使用者可以同時編輯同一篇筆記，而不是各自獨立。這個 change 把即時協作編輯的能力加上去，訂閱等級的解鎖邏輯本身留給後面的 `subscription-billing` change 處理。

## What Changes

- 筆記擁有者可以產生一條唯一的分享連結；已登入的使用者開啟這條連結後，系統自動將其加入這篇筆記的共編者名單。擁有者也可以撤銷連結（讓舊連結失效、可另外產生新連結）或直接移除某位共編者。
- 共編者可以讀取、即時編輯自己被邀請的筆記；刪除筆記與管理共編者名單仍僅限擁有者。
- 多人同時編輯同一篇筆記時，使用 CRDT（Yjs）在前端合併變更，伺服器只負責轉發、不處理合併邏輯。
- 保留筆記的完整編輯歷史，並用定期快照壓縮避免歷史紀錄無限增長。
- 新增 Postgres schema：`NoteCollaborator`（共編者名單）、筆記的 Yjs 更新紀錄與快照。
- 調整既有 `notes` capability 的存取規則：讀取與更新從「僅擁有者」放寬為「擁有者或共編者」；列表從「僅列出自己擁有的筆記」擴充為「也列出被邀請共編的筆記」。

## Capabilities

### New Capabilities
- `collab-editing`：使用者可以透過分享連結加入筆記共編、擁有者可以撤銷連結或移除共編者，多個使用者可以即時協作編輯同一篇筆記，且筆記保留完整編輯歷史。

### Modified Capabilities
- `notes`：讀取、更新、列表這三個既有需求的存取範圍從「僅擁有者」放寬為「擁有者或共編者」（刪除與建立不變，仍僅限擁有者本人）。

## Impact

- 新增 Postgres table：`NoteCollaborator`、分享連結（`ShareToken` 等欄位）、筆記的 Yjs 更新紀錄／快照相關 table。
- 調整既有 Notes API 的存取檢查邏輯（讀取/更新/列表）。
- 新增即時協作用的 SignalR Hub，使用 `setup-infra-and-auth` 已建立的 Redis 當 backplane。
- Angular 前端：`note-linking` change 已導入的 Tiptap 編輯器，這裡加上官方的 Yjs 協作擴充套件；新增邀請/移除共編者的 UI。
- **依賴前提**：這個 change 修改 `notes` capability 的既有需求，但 `notes` capability 目前只存在於尚未實作/歸檔的 `notes-crud` change 裡（`openspec/specs/` 底下還沒有它的正式版本）。實務上建議先完成並歸檔 `notes-crud`，再實作這個 change；規劃/撰寫 spec 現在可以先進行，但這個依賴順序要留意。
