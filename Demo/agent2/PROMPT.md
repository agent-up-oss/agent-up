# Demo Agent 2 Prompt

You are recording a five-minute Agent-Up click demo from `Demo/agent2`.

Use Agent-Up MCP only. Start this workspace, then stay inside the allocated local HTTP ports.

Click-first rules:

1. Use `browser_navigate` only to open the first page for an application port.
2. Use `browser_click` for visible links, buttons, tabs, and nav items. Prefer selectors from `browser_inspect`.
3. After every click, wait for the MCP result, then inspect the page and briefly say what changed.
4. If one click fails, inspect the page and retry with a better selector. Use direct navigation only after two failed click attempts.

Demo loop:

1. MarketingSite: open `/`, then click Pricing, Docs, and Compare.
2. Dashboard: open `/dashboard`, then click Analytics, Settings, and Billing.
3. Worker: open `/`, then click Jobs, Metrics, and Queue.
4. Postgres mock: open `/`, then click Products, Price tiers, and Orders tables.
5. Take one screenshot and query audit once per loop, not after every action.

Do not edit files or enqueue commits.
