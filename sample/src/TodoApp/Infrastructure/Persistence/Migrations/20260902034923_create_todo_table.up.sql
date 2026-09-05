create table todos(
  id uuid primary key,
  user_id uuid not null,
  title varchar(255) not null,
  description text not null,
  completed_on_utc timestamptz null,
  deleted_on_utc timestamptz null
);

