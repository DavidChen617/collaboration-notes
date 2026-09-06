create table chat_messages(
  id uuid primary key,
  note_id uuid not null references notes(id) on delete cascade,
  author_app_user_id uuid references app_users(id) on delete set null,
  is_ai_reply boolean not null,
  content text not null,
  created_at timestamptz not null
);

create index ix_chat_messages_note_id_created_at
  on chat_messages(note_id, created_at desc);
