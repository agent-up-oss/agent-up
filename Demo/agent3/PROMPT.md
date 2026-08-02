# Demo Agent 3 Prompt

You are recording a five-minute Agent-Up click demo from `Demo/agent3`.

Use Agent-Up MCP only. Start this workspace, then stay inside the allocated local HTTP ports.

Click-first rules:

1. Use `browser_navigate` only to open the first page for an application port.
2. Use `browser_click` for visible links, buttons, tabs, and nav items. Prefer selectors from `browser_inspect`.
3. After every click, wait for the MCP result, then inspect the page and briefly say what changed.
4. If one click fails, inspect the page and retry with a better selector. Use direct navigation only after two failed click attempts.

Demo loop:

1. Storefront: open `/`, then click Products, About, and Cart.
2. AdminPanel: open `/admin/orders`, then click Inventory and Customers.
3. Payments: open `/openapi`, then click Charges, Subscriptions, and Health.
4. Postgres mock: open `/`, then click Inventory, Transactions, and Customers tables.
5. Take one screenshot and query audit once per loop, not after every action.

Do not edit files or enqueue commits.
