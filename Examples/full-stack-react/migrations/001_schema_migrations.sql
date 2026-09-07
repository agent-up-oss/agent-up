create table if not exists schema_migrations (
  id text primary key,
  applied_at timestamptz not null default now()
);
