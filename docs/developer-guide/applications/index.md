---
title: Applications
---

<DocEyebrow slice="Applications" status="preview" />

# Applications

<DocFocus>
The Server launches processes, allocates ports, and tickets HTTPS access. Clients never bind application ports themselves.
</DocFocus>

**Owner:** `AgentUp.Server` (`Applications`, `Ports`, `ApplicationProxy`, `DesktopApplications`). Tests live in `AgentUp.Server.Tests/Features/Applications/` and related slices. REST: `/api/workspaces/{id}/applications`, `/api/apps/tickets`, `/apps/{workspaceId}/{port}`. Hosted GUI automation tools are on `/mcp/browser`.

## What it is

Local commands, Docker services, and Linux GUI processes are Server-owned. HTTP tabs connect to allocated loopback ports. Remote clients reach those ports through authenticated tickets. `desktopApplications` run on an isolated Xvfb display.

<DocSpine>
<DocBeat selected>Declare the process in `agent-up.json`</DocBeat>
<DocBeat>Start the workspace so ports are allocated</DocBeat>
<DocBeat>Open HTTP WebViews or the ticketed streamed viewer</DocBeat>
</DocSpine>

<DocContract>POST /api/apps/tickets</DocContract>

Hosted GUI tools on Browser MCP: `desktop_inspect`, `desktop_screenshot`, `desktop_click`, `desktop_fill`, and `desktop_press`. These tools address an explicit workspace application and session generation. Desktop screenshots use the Server-owned framebuffer; coordinate input from a stale generation is rejected after an application restart.

Next in this slice: [Hosting assessment](/developer-guide/applications/hosting-assessment) (not the current contract).

## Process environment

When the Server launches a local application process, it injects the workspace's full allocated port map into the process environment. This lets sibling applications discover each other through declared variables such as `WEB_PORT`, `API_PORT`, and `POSTGRES_PORT` without coupling application source code to Agent-Up APIs. When multiple applications declare the same port variable name, each process still receives its own allocated port for that variable.

Local application commands are parsed as an executable plus arguments and launched with `ProcessStartInfo.ArgumentList`. The Server rejects shell expressions such as pipes, redirects, variable expansion, command chaining, and subshells before process start.

Managed local application processes also receive `AGENT_UP_AUDIT_ENDPOINT`, `AGENT_UP_WORKSPACE_ID`, and `AGENT_UP_APPLICATION`. Browser builds can expose these values to `@agent-up/audit`. The audit endpoint is derived from the orchestrating Server's configured public/listen URL, including the development port, rather than assuming the packaged port.

Process output storage must not use workspace IDs or application names as raw path segments.

## Application proxy

Allocated HTTP application ports stay bound on the Server host. Remote clients reach them through `POST /api/apps/tickets` and the `/apps/{workspaceId}/{port}` bootstrap. The ticket travels in `X-Agent-Up-Ticket` or a URL fragment consumed by a Server-owned bootstrap page, never as a query string. Bootstrap sets an HttpOnly cookie and reverse-proxies unmatched paths to `http://127.0.0.1:{port}`. GET and ticket-consuming bootstrap requests redirect to `/` on the Server origin. Unsafe proxied writes require an Origin that matches the Server scheme, host, and port; writes without an Origin are rejected. Abandoned tickets expire after 30 seconds.

That cookie does not authorize REST or MCP routes. Reserved Server prefixes such as `/api`, `/mcp`, and `/apps` are never forwarded to a workspace application. Proxied `Set-Cookie` values cannot overwrite `agent-up-` or ASP.NET cookies. Only currently listening allocated HTTP ports are forwarded. Ticket issuance and ticket or session acceptance require a TLS connection to the Server, except for loopback development peers. Client-supplied forwarded scheme headers cannot satisfy that check.

The REST API permits cross-origin browser requests from any HTTP or HTTPS origin, so the Mobile web/PWA client can reach a Server the user points it at. The browser's own mixed-content policy still applies.

<DocSurface mobile>Mobile never opens `http://127.0.0.1:{allocatedPort}` on the device. Native WebViews send the ticket in the `X-Agent-Up-Ticket` header. The installable web client appends `#ticket=` so the secret stays off the HTTP request line.</DocSurface>

## Hosted Linux desktop applications

The `DesktopApplications` slice starts a dedicated Xvfb display, creates a private `XDG_RUNTIME_DIR` for that session, and injects `DISPLAY` together with `GDK_BACKEND=x11`, `WAYLAND_DISPLAY=agentup-hosted-no-wayland`, `XDG_SESSION_TYPE=x11`, `GTK_USE_PORTAL=0`, `QT_QPA_PLATFORM=xcb`, and software-GL variables so toolkit autodetect cannot attach to the host Wayland or X11 session. It also injects `LD_LIBRARY_PATH`, merging the Server process path with libraries imported from a nearby `shell.nix` when Nix is present. It captures the root framebuffer as PNG and tears the display down with the application or workspace. Sessions have monotonically changing generations so delayed input from a previous process is rejected. Raw X11 sockets are never exposed.

<DocSurface desktop>Applications whose Server DTO kind is `Desktop` use a separate application sub-tab labeled Desktop. Desktop retries session-scoped viewer-ticket requests while the application is starting or running, and hosts the returned Server viewer in a dedicated NativeWebView. This viewer is never used for ordinary HTTP ports.</DocSurface>

## Console, metrics, and database explorer

<DocSurface desktop>For applications with configured ports, the second tab row starts with ports in `agent-up.json` order. Console and Metrics remain available after the port tabs. The Metrics tab shows summary cards and time-series charts from Server-pulled application metrics (`ports[].metrics`) every 30 seconds while selected. When an application sets `database: true`, Desktop adds a Database explorer tab. The Server owns the queries at `/api/workspaces/{id}/applications/{name}/database/...`.</DocSurface>
