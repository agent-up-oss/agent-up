---
title: Browser
---

# Browser

Every workspace uses two separate browser surfaces:

- **Desktop embedded browser:** Avalonia `NativeWebView` instances that connect directly to each application's allocated HTTP port.
- **Server headless browser:** Chromium automation sessions stored under `browser-profiles/{workspaceId}` and used by MCP browser tools.

These surfaces do not share cookies, local storage, session storage, IndexedDB, cache, or navigation state. Logging in through Desktop does not authenticate the agent headless session, and agent navigation does not change what Desktop shows.

Each surface still keeps its own state per workspace. Browser state includes:

- Cookies.
- Local Storage.
- Session Storage.
- IndexedDB.
- Cache.

Within a workspace, Desktop preserves each HTTP port tab's WebView state when you switch tabs or applications. The Server preserves the headless profile across MCP browser actions and application restarts.

Changing workspaces restores the selected workspace's saved state on each surface independently. Restarting applications should reload the existing Desktop WebView and headless session rather than opening duplicate tabs or profiles.

## Desktop And Agent Browsing

Developers use the Desktop embedded browser to interact with running applications on their allocated HTTP ports. AI agents use the Server headless browser through MCP automation tools. The Desktop does not stream or mirror the headless session, and the two profiles do not synchronize automatically.

## Structured Inspection

Agent-Up exposes browser state to agents through structured inspection instead of requiring users or agents to scrape raw page markup.

Inspection can include:

- Accessibility tree.
- Interactive elements.
- Page metadata.
- DOM snapshot.
- HTML.
- Browser history.
- Screenshot.

Accessibility data should be preferred over raw HTML.

## Validation checks

Use the Validation sidebar of the selected Desktop workspace to open checks for the currently selected application. Each check shows the user journey it validates and can be played in the shared browser, where you can watch the recorded steps and their expected outcomes. Agents can edit or re-record an existing check without losing its identity, and export it as a Playwright test for headless CI.
