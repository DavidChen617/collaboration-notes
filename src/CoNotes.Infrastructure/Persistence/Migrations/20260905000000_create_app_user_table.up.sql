create table app_users(
  id uuid primary key,
  keycloak_sub varchar(255) not null unique,
  created_at timestamptz not null
);
