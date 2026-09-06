create table license_codes(
  id uuid primary key,
  code varchar(64) not null unique,
  plan_tier varchar(10) not null,
  paypal_order_id varchar(64) not null,
  redeemed_by_app_user_id uuid references app_users(id) on delete set null,
  created_at timestamptz not null,
  redeemed_at timestamptz
);
