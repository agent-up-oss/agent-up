# Full Stack React Example

A real React + Express + Postgres workspace that exercises the main Agent-Up product surfaces.

Unlike the lightweight `Demo/` workspaces (dependency-free Node mocks), this example is a normal npm project with SQL migrations, seeded tables, and a Vite React frontend.

## Why the root workspace database looked empty

The root `agent-up.json` starts a Postgres container with `"database": true`, but nothing in that workspace creates schema or seed data. Agent-Up's database viewer only **reads** whatever already exists in Postgres.

This example solves that by running SQL migrations and seed data when the API process starts.

## Agent-Up features covered

| Feature | Where |
|---------|-------|
| Multi-application workspace | `Example Web`, `Example API`, and `Database` apps in the root `agent-up.json` |
| HTTP browser tabs | `Example Web` on `WEB_PORT` |
| Console output | per-app process logs |
| Health checks | `Example API` `/health` |
| Metrics | `Example API` `/metrics` |
| Database explorer | `Database` service with `"database": true` |
| Frontend audit events | `Example Web` uses `@agent-up/audit` |
| Shared env files | `database.env` for `Example API` + `Database` |
| Install commands | `install` runs `npm install` before each start |
| Port allocation | defaults `5600` / `5601` / `5432` |

## Tables created

Migrations live in `migrations/` and are applied in order by `api/migrate.mjs`:

1. `schema_migrations`
2. `products`
3. `orders` and `order_items`

The example keeps `database.env` in this folder. The root workspace references it as `Examples/full-stack-react/database.env`.

## Run it

From the repository root:

```bash
agent-up start
```

The example is wired into the root `agent-up.json` as **Example Web**, **Example API**, and **Database**. There is no separate `agent-up.json` in this folder.

Then in Agent-Up Desktop:

1. Open the **Example Web** browser tab for the React dashboard.
2. Open the **Example API** **Metrics** tab after browsing the UI for a minute.
3. Open the **Database** app **Database** tab and select the `agentup` database.
4. Inspect `products`, `orders`, and `order_items`.

Useful SQL in the database viewer:

```sql
select * from products order by name;
select o.id, o.customer_name, o.status, count(oi.sku) as lines
from orders o
left join order_items oi on oi.order_id = o.id
group by o.id, o.customer_name, o.status;
```

## Project layout

```text
Examples/full-stack-react/
  database.env
  migrations/
  api/
    migrate.mjs
    server.mjs
    seed/
  web/
    src/
```

## Notes

- The API starts immediately and retries Postgres setup in the background, so it can start before the Database container is ready. `/health` returns `503` until migrations and seed data finish.
- If you change the root `agent-up.json` or migrations after a workspace was already registered, run `agent-up start` again so Agent-Up reloads the definition and restarts processes.
- Postgres only applies `database.env` credentials when its Docker volume is first created. If `Example API` or the **Database** tab report authentication failures after a credential change, stop the workspace and remove the example volume, then start again:

```bash
docker volume rm agent-up-full-stack-pgdata
```

If you still have an older example volume from earlier iterations, remove that too:

```bash
docker volume rm agent-up-example-pgdata pgdata 2>/dev/null || true
```
