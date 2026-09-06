alter table notes add column share_token uuid;

create unique index ux_notes_share_token on notes(share_token) where share_token is not null;

create table note_collaborators(
  note_id uuid not null references notes(id) on delete cascade,
  app_user_id uuid not null references app_users(id) on delete cascade,
  primary key (note_id, app_user_id)
);

create index ix_note_collaborators_app_user_id on note_collaborators(app_user_id);
