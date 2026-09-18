---
title: Browser
---

<DocEyebrow slice="Browser" status="preview" />

# Browser

<DocFocus>
Desktop, Mobile, and Server automation do not share cookies, storage, or navigation state.
</DocFocus>

## What it is

Every workspace keeps separate human and automation browser surfaces. Developers use Desktop or Mobile WebViews. Agents use the Server headless profile under `browser-profiles/{workspaceId}`. Restarting applications should reload the existing surface rather than create more tabs.

<DocSpine>
<DocBeat selected>Start the workspace so HTTP ports exist</DocBeat>
<DocBeat>Open the human surface or run an MCP browser action</DocBeat>
<DocBeat>Treat the two profiles as independent browsers</DocBeat>
</DocSpine>

<DocContract>browser-profiles/&#123;workspaceId&#125;</DocContract>

<DocSurface desktop>Embedded `NativeWebView` instances connect directly to each application's allocated HTTP port.</DocSurface>

<DocSurface mobile>The application display uses an authenticated HTTPS reverse proxy on the Server. Tickets travel in a request header or URL fragment, never as a query string.</DocSurface>

<DocSurface mcp>MCP browser tools use the Server headless Chromium profile. They do not read Desktop or Mobile storage.</DocSurface>

Next in this slice: [Validation](/docs/browser/validation).

## Surfaces

| Surface | Owner | Storage | Used by |
|---|---|---|---|
| Desktop embedded browser | Desktop | Native WebView runtime on the workstation | Humans using HTTP port tabs in Agent-Up Desktop |
| Mobile application WebView | Server proxy + client WebView | Independent of Desktop and headless | Humans on Android, iOS, and the PWA |
| Headless automation browser | Server | `browser-profiles/{workspaceId}` | MCP browser tools |

These profiles do not share cookies, local storage, session storage, IndexedDB, cache, or navigation state. A login, cookie, or page visit in Desktop is not visible to the headless session, and agent actions in the headless session do not update Desktop.

## Session continuity

Within a workspace, Desktop preserves each HTTP port tab's WebView state when you switch tabs or applications. The Server preserves the headless profile across MCP browser actions and application restarts.

Changing workspaces restores the selected workspace's saved state on each surface independently. Restarting applications should reload the existing Desktop WebView and headless session rather than opening duplicate tabs or profiles.

## Structured inspection

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
