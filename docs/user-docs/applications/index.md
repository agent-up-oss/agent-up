---
title: Applications
---

<DocEyebrow slice="Applications" status="preview" />

# Applications

<DocFocus>
Applications consume ports through environment variables such as `WEB_PORT`. They never assume a fixed localhost port.
</DocFocus>

## What it is

A workspace runs local processes, Docker services, and hosted Linux GUI processes declared in `agent-up.json`. Console, metrics, and the database explorer belong to the selected application. HTTP applications render in a WebView; `desktopApplications` use a ticketed streamed viewer.

<DocSpine>
<DocBeat selected>Declare the application in `agent-up.json`</DocBeat>
<DocBeat>Start the workspace so the Server allocates ports</DocBeat>
<DocBeat>Open the application surface</DocBeat>
</DocSpine>

<DocContract>ports[].variable</DocContract>

<DocSurface desktop>Selecting an application opens Console, Metrics, Diagnostics, and, when `database` is true, a Postgres explorer. HTTP ports keep a native WebView. `desktopApplications` use a ticketed streamed viewer.</DocSurface>

<DocSurface mobile>Apps lists the same applications and opens each as an inner page. HTTP UIs load through the Server proxy. `desktopApplications` use the streamed viewer.</DocSurface>

Next in this slice: [Configuration](/docs/configuration) for the JSON that declares applications.

## Application tabs

Selecting an application on Desktop opens Console, Metrics, Diagnostics, and, when `database` is true, a Postgres explorer. HTTP ports keep a native WebView. `desktopApplications` use a ticketed streamed viewer instead. Mobile Apps lists the same applications and opens each as an inner page.

The Server owns port allocation and injects the workspace's full allocated port map into each launched local process. Applications should read the relevant environment variables instead of assuming fixed localhost ports.

Docker containers can call host-run applications in the same workspace through `host.agent-up` when the host-run application listens on an address reachable from Docker's host gateway. A process bound only to `127.0.0.1` is not reachable from containers through this alias. Inline Docker environment values may reference allocated workspace ports with `${VARIABLE}`, such as `BACKEND_URL=http://host.agent-up:${API_PORT}`.

## Hosted Linux GUI processes

`desktopApplications` are Linux graphical applications hosted on Server-owned virtual displays and streamed to Desktop and Mobile. Existing HTTP application tabs continue to connect directly to their allocated ports and do not use the streaming path.

## Console and metrics

Application output is a selectable console surface. Prefix install output with `[install]`. Metrics are declared on a port in `agent-up.json` (`ports[].metrics`) and shown as summary cards and time-series charts.
