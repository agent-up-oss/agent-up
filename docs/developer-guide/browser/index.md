---
title: Browser
---

<DocEyebrow slice="Browser" status="preview" />

# Browser

<DocWhat>
Desktop views and Server headless automation are separate sessions bound to the same workspace runtime. They do not share cookies, local storage, session storage, IndexedDB, cache, or navigation state.

The Server owns headless Chromium per workspace. Desktop owns native views for HTTP port tabs. Mobile loads HTTP UIs through the application proxy.
</DocWhat>

<DocMeta
  owner="AgentUp.Server Browser slice"
  tests="AgentUp.Server.Tests/Features/Browser/"
  mcp="/mcp/browser"
/>

<DocSpine>
<DocBeat>Start the workspace and use allocated HTTP ports</DocBeat>
<DocBeat>Navigate with Browser MCP or the human view</DocBeat>
<DocBeat>If a browser action fails, read the workspace console first</DocBeat>
</DocSpine>

<DocContract label="MCP">/mcp/browser</DocContract>

<DocCallout>
After `start_workspace`, use the returned workspace id and allocated ports directly for Browser MCP navigation. If navigation, inspection, waiting, screenshots, or interaction fails or times out, inspect console output first through Orchestration MCP `get_workspace_console`.
</DocCallout>

## Browser MCP

`/mcp/browser` exposes Streamable HTTP and legacy SSE at `/mcp/browser/sse` plus `/mcp/browser/message`. It owns browser navigation, inspection, interaction, wait, screenshot, and validation-flow tools.

<DocSteps>
<DocStep title="Navigate and inspect">
`browser_navigate`, `browser_inspect`, `browser_click`, `browser_fill`, `browser_press`, wait helpers, and `browser_screenshot`.
</DocStep>
<DocStep title="Validation flows">
`save_validation_flow`, `list_validation_flows`, `play_validation_flow`, `export_validation_flow`, and `delete_validation_flow`.
</DocStep>
<DocStep title="Hosted GUI">
`desktop_inspect`, `desktop_screenshot`, `desktop_click`, `desktop_fill`, and `desktop_press`.
</DocStep>
</DocSteps>

`browser_navigate` is restricted to loopback URLs whose port matches one of the workspace's allocated HTTP application ports. Future external redirects, such as OAuth providers, must be represented by explicit allowlist rules rather than arbitrary agent-supplied domains.

`browser_click` is a visible Desktop action. Before executing the DOM click, Desktop activates the application tab for the current browser URL, waits 500 ms, animates the agent mouse marker from its previous position to the target element for 500 ms, shows a 500 ms expanding attention ping, then performs the actual click. The Browser MCP call completes only after this staged click operation and the follow-up page-state read finish.

`browser_screenshot` returns a bounded low-resolution MCP image content block for immediate agent inspection and stores the screenshot as a Server-managed audit artifact. Agents should use the returned artifact id with `/mcp/audit` when they need to reload the screenshot later; they should not request direct access to `/tmp` screenshot paths.

Prefer structured inspection and accessibility data over raw HTML.

## Desktop view hosting

Desktop displays each workspace application through a direct embedded view connection to the allocated HTTP port. Desktop owns those view instances and their browser state on the workstation. The Server owns a separate headless Chromium profile per workspace under `browser-profiles/{workspaceId}`. Desktop does not stream, mirror, or read from the headless session.

Desktop bridges HTML file inputs to the native file picker. The upload bridge and the sign-in popup path are covered end to end in `AgentUp.Tests`. Native view surfaces must be hidden while any in-window modal overlay is visible.

<DocNext href="/developer-guide/browser/validation" title="Validation">
Record, replay, and export a user-meaningful route.
</DocNext>
