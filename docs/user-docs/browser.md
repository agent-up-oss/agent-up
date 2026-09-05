---
title: Browser
---

# Browser

Every workspace owns an isolated browser profile. The Server manages browser lifecycle, profiles, and automation sessions. The Desktop connects directly to each application's allocated HTTP port in an embedded WebView.

Browser state includes:

- Cookies.
- Local Storage.
- Session Storage.
- IndexedDB.
- Cache.

Changing workspaces restores browser state. Restarting applications should reload the existing workspace browser session instead of creating new tabs.

## Desktop And Agent Browsing

Developers use the Desktop embedded browser to interact with running applications on their allocated HTTP ports. AI agents use the Server headless browser through MCP automation tools. The Desktop does not stream or mirror the headless session.

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
