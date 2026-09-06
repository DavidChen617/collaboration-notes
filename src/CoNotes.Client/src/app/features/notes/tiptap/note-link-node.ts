import { mergeAttributes, Node } from '@tiptap/core';

/**
 * An inline, atomic node representing a wikilink to another of the user's notes.
 * Stores the target note's ID (stable across renames) and renders its current title as
 * a `label` attribute - see note-linking design.md decision 2. Serializes as
 * `<span data-note-link="{noteId}">{label}</span>`, which is exactly the format
 * `NoteLinkContentParser` on the backend parses back out (see design.md decision 3).
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
