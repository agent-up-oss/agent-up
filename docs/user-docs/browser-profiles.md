---
title: Browser Profiles
---

# Browser Profiles

Browser profiles isolate workspace state while allowing humans and AI agents to share the same session within a workspace.

## Session Sharing

Developers and AI agents should interact with the same browser session.

Benefits include:

- Shared authentication.
- Shared cookies.
- Shared navigation.
- Shared application state.
- No duplicate browser windows.
- No Playwright browser startup for validation.

## Workspace Isolation

Each workspace has its own profile. Authentication and local browser state should not leak between agents, branches, or worktrees.

## Restart Behavior

Restarting an application should reload the relevant browser session rather than opening another browser tab or creating another profile.
