---
title: Browser
---

# Browser

Every workspace owns an isolated browser profile. The Server manages browser instances and the Desktop displays them.

Browser state includes:

- Cookies.
- Local Storage.
- Session Storage.
- IndexedDB.
- Cache.

Changing workspaces restores browser state. Restarting applications should reload the existing browser session instead of creating new tabs.

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
