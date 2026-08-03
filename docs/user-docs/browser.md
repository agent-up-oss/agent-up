---
title: Browser
---

# Browser

Every workspace owns an isolated browser profile. The Server manages browser instances, RDP remote display, and input ownership. The Desktop displays the Server-owned session.

Browser state includes:

- Cookies.
- Local Storage.
- Session Storage.
- IndexedDB.
- Cache.

Changing workspaces restores browser state. Restarting applications should reload the existing browser session instead of creating new tabs.

## Human And AI Control

Browser control can move between humans and AI agents. Human mode follows the Desktop viewer size so pointer, wheel, and keyboard input align with what is visible. AI mode uses a standardized viewport preset so automation returns to a predictable browser size.

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
