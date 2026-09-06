## Aggregate 邊界

這個 change 不新增 Aggregate，而是替既有的 `Note` Aggregate（`notes-crud` 建立）擴充不變條件：一篇筆記能連到哪些其他筆記、連結要不要隨標題變更保持有效、刪除筆記時連結要不要連帶清除。這些規則都圍繞著單一 `Note` 實例的內容與生命週期，沒有跨越多個筆記的一致性邊界（連結雙方各自的 `Note` 仍是獨立 Aggregate，只是互相以識別碼參照），所以 `NoteLink` 設計成 `Note` Aggregate 底下的關聯實體，不獨立成一個 Aggregate Root。

## Domain Events

- **`NoteLinkedTo`**：儲存筆記內容、解析出新的連結節點時，針對每個新增的連結關係發出（`SourceNoteId`、`TargetNoteId`）。目前沒有其他 context 監聽，但保留這個事件供之後（例如統計、通知）掛上去。
- **`NoteLinkRemoved`**：儲存筆記內容時偵測到某個連結節點被移除、或刪除筆記連帶清除連結時發出。

## 執行流程

```mermaid
sequenceDiagram
    participant U as 使用者
    participant Api as Api (Endpoint)
    participant Cmd as SaveNoteCommandHandler
    participant Note as Note Aggregate
    participant Repo as NoteLink Repository
    participant Evt as Domain Event Handler

    U->>Api: 輸入 [[ 觸發自動完成、選定筆記、儲存內容
    Api->>Cmd: SaveNoteCommand
    Cmd->>Note: 解析內容中的連結節點
    Note-->>Cmd: 驗證每個連結目標皆為使用者自己的筆記
    Cmd->>Repo: 交易內刪除既有連結、寫入目前解析到的連結
    Repo-->>Cmd: 完成
    Cmd->>Evt: 發出 NoteLinkedTo / NoteLinkRemoved
```

## Context

見 proposal.md 的 Why。建立在 `notes-crud` 已完成的 `notes` capability 之上（`Note` table、擁有權模型已存在）。目前 Angular 端的筆記編輯器還是陽春的文字輸入，這個 change 是第一次需要「編輯器內插入一個顯示文字跟底層資料不同的行內元素」這種需求。另外要考慮：之後 `collab-editing` change 會需要用 Yjs 做即時共編，這裡選的編輯器框架最好能延續到那個階段，不要之後重寫。

## Goals / Non-Goals

**Goals:**
- 輸入 `[[` 觸發自動完成，只列出使用者自己的筆記。
- 選定後插入的連結，顯示當下標題、底層存筆記識別碼——筆記改標題不會弄斷連結。
- 提供一個關係圖檢視，呈現使用者所有筆記（節點）與連結（邊）。
- 刪除筆記時，相關連結一併清除，不留下指向不存在筆記的連結。

**Non-Goals:**
- 即時多人共編（`collab-editing` change 的範圍）——這裡選的編輯器框架要能承接那個需求，但這個 change 本身不做即時同步。
- 跨使用者的連結——目前筆記還沒有共享機制，連結只會發生在同一個使用者自己的筆記之間。

## Decisions

**1. 編輯器框架選 Tiptap（基於 ProseMirror）。**
選它的關鍵原因：Tiptap 官方直接提供 Yjs 協作擴充套件（`@tiptap/extension-collaboration`，見 [Tiptap Collaboration 官方文件](https://tiptap.dev/docs/editor/extensions/functionality/collaboration)、[Yjs 官方文件裡的 Tiptap 整合](https://docs.yjs.dev/ecosystem/editor-bindings/tiptap2)），代表之後 `collab-editing` change 要接上即時共編時，不需要換編輯器框架。曾考慮純 `textarea`/`contenteditable`（否決：沒有乾淨的方式做「顯示文字跟底層資料不同」的行內元素）；也考慮過 Quill、Slate（否決：沒有像 Tiptap/ProseMirror 這樣官方且成熟的 Yjs 綁定）。

**2. 連結用 Tiptap 的 Mention 風格行內節點實作，節點屬性存筆記識別碼，顯示文字即時解析成該筆記目前的標題。**
Tiptap 官方的 Suggestion 工具（[Suggestion utility 官方文件](https://tiptap.dev/docs/editor/api/utilities/suggestion)）支援自訂觸發字元，機制上可以用來做「輸入某個字元跳出選單」這種行為。`[[` 這種雙字元觸發的確切設定方式留到實作階段驗證（見 tasks.md），必要時退而求其次用單一 `[` 觸發也能達到相同使用體驗。

**3. `NoteLink` 的紀錄採「儲存時重新整理」策略：每次儲存筆記內容，解析內容裡目前有哪些連結節點，用一個交易（transaction）把該筆記為來源的既有 `NoteLink` 全部刪除、重新寫入目前解析到的連結。**
不用即時、逐筆增刪的方式追蹤連結變化，改成「每次儲存都以內容為準重新整理」，邏輯簡單很多，不用處理「使用者刪掉一個連結」這種細粒度事件。

**4. 關係圖資料由一個 API endpoint 即時查詢組出（使用者所有筆記當節點、所有 `NoteLink` 當邊），不做額外的快取或預先計算的圖結構。**
以玩具規模的筆記數量，即時查詢完全足夠，不需要引入圖形資料庫或快取層的複雜度。

**5. 前端關係圖渲染選 Cytoscape.js。**
框架無關的圖形視覺化函式庫，內建 force-directed 版面配置，可以直接嵌入 Angular component 使用，不需要額外的 Angular 專屬綁定。

## Risks / Trade-offs

- **[Risk]** 「儲存時整批刪除重寫連結」如果儲存中途失敗，可能留下不完整的連結狀態。→ **Mitigation**：整個刪除+寫入包在同一個資料庫交易裡，失敗就整個 rollback，不會留下部分狀態。
- **[Risk]** Tiptap 的 Suggestion 工具能不能乾淨支援 `[[` 這種雙字元觸發，還沒實際驗證過。→ **Mitigation**：留到實作階段確認；備案是用單一 `[` 當觸發字元，使用者體驗上差異不大。
- **[Risk]** 使用者筆記數量變多時，關係圖畫面可能變得雜亂或渲染變慢。→ **Mitigation**：玩具/個人使用規模下可以接受，不在這個 change 裡處理效能優化。
