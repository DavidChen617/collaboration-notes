create table note_updates(
  note_id uuid not null references notes(id) on delete cascade,
  sequence_number bigint not null,
  update_payload bytea not null,
  created_at timestamptz not null,
  primary key (note_id, sequence_number)
);

create table note_snapshots(
  note_id uuid not null references notes(id) on delete cascade,
  sequence_number bigint not null,
  snapshot_payload bytea not null,
  created_at timestamptz not null,
  primary key (note_id, sequence_number)
);
