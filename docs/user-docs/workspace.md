---
title: Workspace
---

# Workspace

A workspace is the unit of isolation in Agent-Up.

Each workspace contains:

- Repository.
- Git worktree.
- Branch.
- Commit.
- Browser profile.
- Docker infrastructure.
- Running processes.
- Allocated port range.
- Runtime diagnostics.
- Event history.

## Worktree Model

Every AI agent operates inside its own Git worktree. This allows multiple agents to change the same repository independently while preserving separate branches, running applications, browser sessions, and validation state.

## Runtime Isolation

Workspace isolation prevents collisions between concurrent development sessions. A workspace receives its own port range, process group, Docker lifecycle, browser profile, and diagnostics stream.

## Switching Workspaces

Switching workspaces should restore the relevant running applications and browser state without forcing developers or AI agents to recreate tabs, reauthenticate, or restart unrelated services.
