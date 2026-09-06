create table note_links(
  source_note_id uuid not null references notes(id) on delete cascade,
  target_note_id uuid not null references notes(id) on delete cascade,
  primary key (source_note_id, target_note_id)
);

create index ix_note_links_target_note_id on note_links(target_note_id);
