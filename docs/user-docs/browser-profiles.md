---
title: Browser Profiles
---

# Browser Profiles

Browser profiles isolate workspace state. Agent-Up keeps separate profiles for Desktop browsing and Server headless automation.

## Desktop And Headless Profiles

Each workspace has two independent browser profiles:

| Surface | Owner | Storage | Used by |
|---|---|---|---|
| Desktop embedded browser | Desktop | Native WebView runtime on the workstation | Humans using HTTP port tabs in Agent-Up Desktop |
| Headless automation browser | Server | `browser-profiles/{workspaceId}` | MCP browser tools and other Server automation |

These profiles do not share cookies, local storage, session storage, IndexedDB, cache, or navigation state. A login, cookie, or page visit in Desktop is not visible to the headless session, and agent actions in the headless session do not update Desktop.

## Headless Session Continuity

Within a workspace, MCP browser actions reuse the same Server headless profile stored under `browser-profiles/{workspaceId}`. That gives agents shared authentication, navigation, and application state across automation steps without starting a new browser for every tool call.

## Desktop Session Continuity

Within a workspace, Desktop keeps one embedded WebView per HTTP port tab. Switching tabs or applications preserves each WebView's page state instead of reloading it unnecessarily.

## Workspace Isolation

Each workspace has its own Desktop WebViews and its own Server headless profile directory. Authentication and browser state must not leak between workspaces, branches, or worktrees.

## Restart Behavior

Restarting an application should reload the relevant Desktop WebView and Server headless session for that workspace rather than opening another browser tab or creating another profile.
