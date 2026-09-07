create table if not exists products (
  sku text primary key,
  name text not null,
  category text not null,
  status text not null,
  region text not null,
  inventory integer not null,
  unit_price numeric(10, 2) not null,
  margin numeric(5, 2) not null,
  updated_at timestamptz not null
);

create index if not exists products_category_idx on products (category);
create index if not exists products_status_idx on products (status);
