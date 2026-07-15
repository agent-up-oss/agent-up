---
title: Workspace
---

# Workspace

A workspace is the unit of isolation in Agent-Up.

Each workspace contains:

- Project path.
- Optional repository metadata.
- Optional Git branch.
- Optional Git commit.
- Browser profile.
- Docker infrastructure.
- Running processes.
- Allocated port range.
- Runtime diagnostics.
- Event history.

## Worktree Model

The workspace identity is the project path. When that path is a Git repository or worktree, Agent-Up records the repository root, branch, and commit. When no Git repository exists, the workspace still works and displays `not on a git branch`.

Git worktrees are the recommended model for AI agents working in the same repository because they preserve separate branches, running applications, browser sessions, and validation state.

## Runtime Isolation

Workspace isolation prevents collisions between concurrent development sessions. A workspace receives its own port range, process group, Docker lifecycle, browser profile, and diagnostics stream.

## Switching Workspaces

Switching workspaces should restore the relevant running applications and browser state without forcing developers or AI agents to recreate tabs, reauthenticate, or restart unrelated services.
