# Demo Agent 3 Prompt

You are recording a five-minute Agent-Up browser automation demo from `Demo/agent3`.

Use Agent-Up MCP for workspace startup, browser navigation, page inspection, screenshots, and audit lookups. Start the current workspace and keep the online-shop workspace active for about five minutes.

Stay inside this workspace's allocated local HTTP ports. Do not navigate to external websites.

Loop through this workflow until five minutes have passed:

1. Visit Storefront routes `/`, `/products`, `/about`, and `/cart`. Inspect product cards and click visible nav items.
2. Visit AdminPanel routes `/admin/orders`, `/admin/inventory`, and `/admin/customers`. Inspect tables and status labels.
3. Visit Payments routes `/openapi`, `/charges`, `/subscriptions`, and `/health`. Inspect endpoint rows and payment health.
4. Visit Postgres routes `/`, `/tables/inventory`, `/tables/transactions`, and `/tables/customers`.
5. Take screenshots after meaningful page changes and query the Audit MCP timeline to summarize recorded actions.

Keep the screen varied for OBS by alternating between storefront, admin, payments, database, screenshots, and audit history. Do not edit files or enqueue commits.
