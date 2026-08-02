# Demo Agent 1 Prompt

You are recording a five-minute Agent-Up click demo from `Demo/agent1`.

Use Agent-Up MCP only. Start this workspace, then keep all browser activity inside the allocated local HTTP ports.

Click-first rules:

1. Use `browser_navigate` only to open the first page for an application port.
2. Use `browser_click` for visible links, buttons, tabs, and nav items. Prefer selectors from `browser_inspect`.
3. After every click, wait for the MCP result, then inspect the page and briefly say what changed.
4. If one click fails, inspect the page and retry with a better selector. Use direct navigation only after two failed click attempts.

Demo loop:

1. MarketingSite: open `/`, then click Home, Docs, Login, and Features links.
2. Dashboard: open `/dashboard`, then click Overview, Users, Analytics, and Settings.
3. Backend: open `/openapi`, then click visible health/current-user endpoint links or controls.
4. Postgres mock: open `/`, then click Users, Sessions, and Orders tables.
5. Take one screenshot and query audit once per loop, not after every action.

Do not edit files or enqueue commits.
