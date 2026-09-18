---
title: Mobile
---

# Mobile

Agent-Up Mobile is the Expo client for Android, iOS, and the installable web PWA. It displays Server-owned workspace state and submits requests. It does not own orchestration.

Connect with the Server URL first. Saved servers stay on the device; only one is active. Switching servers drops that client's local workspace state. Remote servers must use HTTPS. Loopback HTTP remains for local development.

After connect, the sidebar lists workspaces for the active Server. The `+` control clones a repository into the Server source-clones directory. There is no Workspaces tab.

Each workspace has a bottom bar:

- **Apps** lists applications. Opening one loads its HTTP UI through the Server proxy, or a streamed viewer for `desktopApplications`.
- **Git** holds branch selection, Fetch/Pull/Push, a compact change list, and a History summary. **Review** is the full change list, diffs, discard, commit, and the read-only agent proposal queue. **History** is the commit graph. Both return through the nav-bar back button.
- **Agents** lists Server-discovered ACP agents and opens chat as an inner page. Desktop's matching chrome is the **Agent** tab for the live session.

See [Git changes](./git-changes.md), [Setup](./setup.md), and [Browser](./browser.md).
