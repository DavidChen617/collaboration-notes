import { Extension } from '@tiptap/core';
import Suggestion, { SuggestionOptions } from '@tiptap/suggestion';

export interface NoteLinkSuggestionItem {
  noteId: string;
  title: string;
}

export interface NoteLinkSuggestionConfig {
  searchNotes: (keyword: string) => Promise<NoteLinkSuggestionItem[]>;
}

/**
 * Renders the autocomplete popup as a plain positioned <ul> - no component framework
 * binding, just DOM manipulation, consistent with this project not having any visual
 * design/styling yet (see note-linking design.md decision 2/tasks.md 5.2).
 */
function createRenderer() {
  let element: HTMLUListElement | null = null;
  let items: NoteLinkSuggestionItem[] = [];
  let selectedIndex = 0;
  let onSelect: ((item: NoteLinkSuggestionItem) => void) | null = null;

  const renderItems = () => {
    if (!element) return;
    element.innerHTML = '';
    items.forEach((item, index) => {
      const li = document.createElement('li');
      li.textContent = item.title;
      li.style.fontWeight = index === selectedIndex ? 'bold' : 'normal';
      li.addEventListener('mousedown', (event) => {
        event.preventDefault();
        onSelect?.(item);
      });
      element!.appendChild(li);
    });
  };

  return {
    onStart: (props: { items: NoteLinkSuggestionItem[]; clientRect?: (() => DOMRect | null) | null; command: (item: NoteLinkSuggestionItem) => void }) => {
      items = props.items;
      selectedIndex = 0;
      onSelect = props.command;

      element = document.createElement('ul');
      element.setAttribute('data-note-link-suggestion', 'true');
      element.style.position = 'absolute';
      element.style.background = 'white';
      element.style.border = '1px solid black';
      element.style.listStyle = 'none';
      element.style.padding = '0.25rem';
      element.style.margin = '0';
      document.body.appendChild(element);

      const rect = props.clientRect?.();
      if (rect) {
        element.style.left = `${rect.left}px`;
        element.style.top = `${rect.bottom}px`;
      }

      renderItems();
    },
    onUpdate: (props: { items: NoteLinkSuggestionItem[]; clientRect?: (() => DOMRect | null) | null }) => {
      items = props.items;
      const rect = props.clientRect?.();
      if (rect && element) {
        element.style.left = `${rect.left}px`;
        element.style.top = `${rect.bottom}px`;
      }
      renderItems();
    },
    onKeyDown: (props: { event: KeyboardEvent }) => {
      if (props.event.key === 'ArrowDown') {
        selectedIndex = (selectedIndex + 1) % Math.max(items.length, 1);
        renderItems();
        return true;
      }
      if (props.event.key === 'ArrowUp') {
        selectedIndex = (selectedIndex - 1 + Math.max(items.length, 1)) % Math.max(items.length, 1);
        renderItems();
        return true;
      }
      if (props.event.key === 'Enter') {
        if (items[selectedIndex]) onSelect?.(items[selectedIndex]);
        return true;
      }
      if (props.event.key === 'Escape') {
        element?.remove();
        element = null;
        return true;
      }
      return false;
    },
    onExit: () => {
      element?.remove();
      element = null;
    },
  };
}

export const NoteLinkSuggestion = Extension.create<NoteLinkSuggestionConfig>({
  name: 'noteLinkSuggestion',

  addOptions() {
    return {
      searchNotes: () => Promise.resolve([]),
    };
  },

  addProseMirrorPlugins() {
    const options: Partial<SuggestionOptions<NoteLinkSuggestionItem>> = {
      editor: this.editor,
      char: '[',
      items: async ({ query }) => this.options.searchNotes(query),
      render: createRenderer,
      command: ({ editor, range, props }) => {
        editor
          .chain()
          .focus()
          .deleteRange(range)
          .insertContent({ type: 'noteLink', attrs: { noteId: props.noteId, label: props.title } })
          .run();
      },
    };

    return [Suggestion(options as SuggestionOptions)];
  },
});
