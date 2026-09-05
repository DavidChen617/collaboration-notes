**Bounded Context**: Notes｜**Aggregate**: `Note`

## Why

在共編、AI Chat、訂閱這些後續階段之前，需要先有「筆記」這個核心功能存在——使用者要能建立、編輯、刪除自己的筆記，這是 Free tier 的基礎功能，也是後面每個更高等級（Pro 的共編、ProMax 的 AI Chat）都要疊加上去的對象。

## What Changes

- 新增 Notes API：建立筆記、列出目前使用者自己的筆記、讀取單一筆記、更新筆記內容、刪除筆記。
- 筆記歸屬於建立它的使用者（`AppUser`），目前僅限本人存取——共享編輯是後面 `collab-editing` change 的範圍，這裡不處理。
- 新增 Postgres schema：`Note` table，關聯到 `identity/authentication` capability 已建立的 `AppUser`。
- Angular SPA 新增筆記列表與編輯畫面（建立、編輯、刪除筆記的基本 UI）。

## Capabilities

### New Capabilities
- `notes`：使用者可以建立、讀取、更新、刪除自己擁有的筆記；非擁有者無法存取。後續 `collab-editing` change 會在這個 capability 上疊加共享編輯的需求。

### Modified Capabilities
（無）

## Impact

- 新增 Postgres table：`Note`（擁有者外鍵指向 `AppUser`）。
- 新增 API endpoint：`Notes` 相關的 CRUD。
- 新增 Angular 畫面：筆記列表、筆記編輯器。
- 依賴 `setup-infra-and-auth` 已建立的 `identity/authentication` capability——所有 Notes endpoint 都要求已驗證身份，且假設呼叫者已有對應的 `AppUser` 記錄。
