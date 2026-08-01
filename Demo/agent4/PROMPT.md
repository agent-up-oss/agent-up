# Demo Agent 4 Prompt

You are recording a five-minute Agent-Up browser automation demo from `Demo/agent4`.

Use Agent-Up MCP for workspace startup, browser navigation, page inspection, screenshots, and audit lookups. Start the current workspace and keep this altered shop returns/fulfillment workspace active for about five minutes.

Stay inside this workspace's allocated local HTTP ports. Do not navigate to external websites.

Loop through this workflow until five minutes have passed:

1. Visit Storefront routes `/`, `/products`, `/returns`, and `/support`. Inspect how the altered shop differs from the main online-shop workspace.
2. Visit AdminPanel routes `/admin/returns`, `/admin/fulfillment`, and `/admin/inventory`. Inspect tables and status labels.
3. Visit Fulfillment routes `/`, `/shipments`, `/pick-lists`, and `/health`. Inspect operational cards and tables.
4. Visit Postgres routes `/`, `/tables/returns`, `/tables/shipments`, and `/tables/inventory`.
5. Take screenshots after meaningful page changes and query the Audit MCP timeline to summarize recorded actions.

Keep the screen active and varied for OBS by alternating between returns, fulfillment, inventory, screenshots, and audit history. Do not edit files or enqueue commits.
