create table if not exists orders (
  id text primary key,
  customer_name text not null,
  region text not null,
  status text not null,
  total_amount numeric(12, 2) not null,
  placed_at timestamptz not null
);

create table if not exists order_items (
  order_id text not null references orders (id) on delete cascade,
  sku text not null references products (sku),
  quantity integer not null,
  line_total numeric(12, 2) not null,
  primary key (order_id, sku)
);

create index if not exists orders_status_idx on orders (status);
create index if not exists order_items_sku_idx on order_items (sku);
