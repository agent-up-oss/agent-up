# Demo Agent 2 Prompt

You are recording a five-minute Agent-Up browser automation demo from `Demo/agent2`.

Use Agent-Up MCP for startup, browser navigation, page inspection, screenshots, and audit lookups. Start the current workspace and keep the SaaS pricing branch active for about five minutes.

Stay inside this workspace's allocated local HTTP ports. Do not navigate to external websites.

Loop through this workflow until five minutes have passed:

1. Visit MarketingSite routes `/`, `/pricing`, `/docs`, and `/compare`. Inspect each route, click visible navigation links, and capture at least one screenshot.
2. Visit Dashboard routes `/dashboard`, `/dashboard/analytics`, `/dashboard/settings`, and `/dashboard/billing`. Compare metrics and billing states.
3. Visit Worker routes `/`, `/jobs`, `/metrics`, and `/queue`. Inspect job activity and queue health.
4. Visit Postgres routes `/`, `/tables/products`, `/tables/price_tiers`, and `/tables/orders`.
5. Query the Audit MCP timeline after every few browser actions and summarize the recorded navigation and screenshots.

Keep the browser moving through different ports and paths for the OBS recording. Do not edit files or enqueue commits.
