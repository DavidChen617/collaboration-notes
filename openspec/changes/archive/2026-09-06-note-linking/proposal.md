**Bounded Context**: Notes｜**Aggregate**: `Note`

## Why

單純的筆記 CRUD（`notes-crud`）只讓筆記彼此獨立存在。使用者想要用類似 Obsidian 的雙向連結（wikilink）把相關筆記串起來，並用關係圖看到自己筆記之間的關聯，這是筆記類產品很核心的差異化功能。

## What Changes

- 筆記編輯畫面：輸入 `[[` 時跳出自動完成清單，列出使用者自己既有的筆記，選中後在內容裡插入一個連結——畫面上顯示筆記標題，底層實際存的是筆記識別碼（重新命名筆記不會弄斷連結）。
- 新增 Postgres schema：`NoteLink` table，記錄筆記之間的連結關係（來源筆記 → 目標筆記）。
- 新增 API：依標題搜尋自己的筆記（給自動完成用）、取得目前使用者所有筆記與連結組成的關係圖資料。
- 儲存筆記內容時，系統重新解析內容裡的連結，同步更新 `NoteLink` 的紀錄。
- 刪除筆記時，一併移除所有跟這篇筆記相關的連結。
- Angular 新增一個關係圖檢視畫面，用節點/邊呈現使用者所有筆記與連結。

## Capabilities

### New Capabilities
- `note-linking`：使用者可以在筆記內容中建立連結到自己的其他筆記（以識別碼為準、顯示當下標題），並可檢視自己所有筆記與連結組成的關係圖。

### Modified Capabilities
（無 —— `notes` capability 的 CRUD 行為不變，連結是疊加在既有筆記內容上的新能力）

## Impact

- 新增 Postgres table：`NoteLink`（`SourceNoteId`、`TargetNoteId`，皆為 `Note` 的外鍵）。
- 新增／調整 API endpoint：筆記標題搜尋、關係圖資料、筆記儲存邏輯（需同步重新計算連結）、筆記刪除邏輯（需同步清除相關連結）。
- Angular 前端：筆記編輯器需要換成支援「輸入觸發字元跳出選單、插入顯示標題但儲存識別碼的連結」的富文字編輯器；新增關係圖檢視畫面。
- 依賴 `notes-crud` 已建立的 `notes` capability（筆記本身、擁有權模型）。
