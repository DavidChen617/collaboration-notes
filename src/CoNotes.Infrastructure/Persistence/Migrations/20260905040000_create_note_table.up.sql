create table notes(
  id uuid primary key,
  owner_app_user_id uuid not null references app_users(id),
  title varchar(255) not null,
  content text not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create index ix_notes_owner_app_user_id on notes(owner_app_user_id);
