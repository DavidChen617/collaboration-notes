import { mergeAttributes, Node } from '@tiptap/core';

/**
 * 代表連到使用者另一篇筆記的 wikilink, 是 inline、atomic 的 node。
 * 儲存目標筆記的 ID(改標題後仍保持不變), 並用 `label` attribute
 * 顯示它目前的標題 - 見 note-linking design.md decision 2。序列化後是
 * `<span data-note-link="{noteId}">{label}</span>`, 這正是 backend 的
 * `NoteLinkContentParser` 解析回來時所依據的格式(見 design.md decision 3)。
 */
export const NoteLinkNode = Node.create({
  name: 'noteLink',
  group: 'inline',
  inline: true,
  atom: true,
  selectable: false,

  addAttributes() {
    return {
      noteId: { default: null },
      label: { default: '' },
    };
  },

  parseHTML() {
    return [{ tag: 'span[data-note-link]' }];
  },

  renderHTML({ node, HTMLAttributes }) {
    return [
      'span',
      mergeAttributes(HTMLAttributes, { 'data-note-link': node.attrs['noteId'] }),
      node.attrs['label'],
    ];
  },
});
