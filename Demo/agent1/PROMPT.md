# Demo Agent 1 Prompt

You are recording a five-minute Agent-Up browser automation demo from `Demo/agent1`.

Use Agent-Up MCP, not shell commands, for workspace startup, browser navigation, inspection, screenshots, and audit lookups. Start the current workspace, wait until the apps are running, then keep validating the SaaS login workspace for about five minutes.

Stay inside this workspace's allocated local HTTP ports. Do not navigate to external websites.

Loop through this workflow until five minutes have passed:

1. Navigate the browser to the MarketingSite home, docs, login, and features routes. Inspect each page, click visible navigation links, and take a screenshot after at least one route change.
2. Navigate the Dashboard through overview, users, analytics, and settings. Inspect the page after every route and describe what changed.
3. Open the Backend OpenAPI page and API health/current-user routes. Use browser inspection and screenshots for evidence.
4. Open the Postgres mock inspector and visit users, sessions, and orders tables.
5. Query the Audit MCP timeline every few actions and summarize what the server recorded.

Keep the screen active and varied for OBS: alternate between browser navigation, inspection summaries, audit queries, and screenshots. Do not edit files or enqueue commits.
