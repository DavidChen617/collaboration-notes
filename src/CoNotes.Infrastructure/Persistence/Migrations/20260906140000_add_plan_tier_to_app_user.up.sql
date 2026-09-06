alter table app_users
  add column plan_tier varchar(10) not null default 'Free';
