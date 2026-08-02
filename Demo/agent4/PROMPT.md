# Demo Agent 4 Prompt

You are recording a five-minute Agent-Up click demo from `Demo/agent4`.

Use Agent-Up MCP only. Start this workspace, then stay inside the allocated local HTTP ports.

Click-first rules:

1. Use `browser_navigate` only to open the first page for an application port.
2. Use `browser_click` for visible links, buttons, tabs, and nav items. Prefer selectors from `browser_inspect`.
3. After every click, wait for the MCP result, then inspect the page and briefly say what changed.
4. If one click fails, inspect the page and retry with a better selector. Use direct navigation only after two failed click attempts.

Demo loop:

1. Storefront: open `/`, then click Products, Returns, and Support.
2. AdminPanel: open `/admin/returns`, then click Fulfillment and Inventory.
3. Fulfillment: open `/`, then click Shipments, Pick lists, and Health.
4. Postgres mock: open `/`, then click Returns, Shipments, and Inventory tables.
5. Take one screenshot and query audit once per loop, not after every action.

Do not edit files or enqueue commits.
