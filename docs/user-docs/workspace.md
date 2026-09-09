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

## Managed Source Clones

Agent-Up can also manage the clone itself instead of only registering a checkout you created. Both the Desktop workspace list and the Mobile Workspaces tab have a `+` button that asks for a repository and a branch. Agent-Up clones that repository at that branch into its source clones directory and registers the resulting workspace, so it appears in the workspace list on every connected client.

The repository must be an `http`, `https`, `ssh`, or `git` remote URL, or the `user@host:path` form. The branch must already exist on the remote. Agent-Up refuses a clone when a directory with the repository's name already exists under the source clones directory.

The source clones directory defaults to a `sources` folder inside the Server data directory. Set `AGENTUP_SOURCE_CLONES_ROOT` in the Server environment to keep managed clones somewhere else, such as a larger disk. Clones created this way behave like any other workspace: they get their own port range, processes, browser profile, and diagnostics.

Removing such a workspace unregisters it from Agent-Up. It does not delete the clone from disk.

## Runtime Isolation

Workspace isolation prevents collisions between concurrent development sessions. A workspace receives its own port range, process group, Docker lifecycle, browser profile, and diagnostics stream.

## Switching Workspaces

Switching workspaces should restore the relevant running applications and browser state without forcing developers or AI agents to recreate tabs, reauthenticate, or restart unrelated services.
