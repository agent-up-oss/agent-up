---
title: Workspaces
---

<DocEyebrow slice="Workspaces" status="available" />

# Workspaces

<DocFocus>
A workspace is the unit of isolation for a developer or agent session. Start it from the directory that contains `agent-up.json`.
</DocFocus>

## What it is

A workspace is identified by project path. It may include Git metadata, a browser profile, Docker infrastructure, running processes, an allocated port range, diagnostics, and event history. Non-Git paths still work and display as `not on a git branch`. Agent-Up can also clone a repository for you and register the result.

<DocSpine>
<DocBeat selected>Add `agent-up.json` at the repository root</DocBeat>
<DocBeat>Start the workspace</DocBeat>
<DocBeat>Switch among isolated sessions without colliding ports or browser state</DocBeat>
</DocSpine>

<DocContract>agent-up start</DocContract>

The installed CLI is `agent-up`. The server URL defaults to `http://localhost:5000` when packaged, or `http://localhost:5001` for the repository launch profile.

<DocSurface cli>From the directory that contains `agent-up.json`:</DocSurface>

```bash
agent-up start --server http://localhost:5001
```

<DocSurface desktop mobile>The workspace list `+` control clones a repository at a branch into the Server source-clones directory and registers it.</DocSurface>

Next in this slice: [Applications](/docs/applications) for the processes that run inside a workspace.

## Worktree model

The workspace identity is the project path. When that path is a Git repository or worktree, Agent-Up records the repository root, branch, and commit. When no Git repository exists, the workspace still works and displays `not on a git branch`.

Git worktrees are the recommended model for AI agents working in the same repository because they preserve separate branches, running applications, browser sessions, and validation state.

<DocSurface desktop>The Overview tab has a branch dropdown for local and remote-tracking branches, Fetch/Pull/Push, plus workspace identity and Server-owned CPU, memory, storage, and process totals.</DocSurface>

<DocSurface mobile>Those Git controls live on the workspace Git tab, beside Apps and Agents.</DocSurface>

## Managed source clones

Agent-Up can manage the clone itself instead of only registering a checkout you created. Both the Desktop workspace list and the Mobile sidebar workspace list have a `+` button that asks for a repository and a branch. Agent-Up clones that repository at that branch into its source clones directory and registers the resulting workspace, so it appears in the workspace list on every connected client.

The repository must be an `http`, `https`, `ssh`, or `git` remote URL, or the `user@host:path` form. The branch must already exist on the remote. Agent-Up refuses a clone when a directory with the repository's name already exists under the source clones directory.

The source clones directory defaults to a `sources` folder inside the Server data directory. Set `AGENTUP_SOURCE_CLONES_ROOT` in the Server environment to keep managed clones somewhere else, such as a larger disk. Clones created this way behave like any other workspace: they get their own port range, processes, browser profile, and diagnostics.

Removing such a workspace unregisters it from Agent-Up. It does not delete the clone from disk.

## Runtime isolation

Workspace isolation prevents collisions between concurrent development sessions. A workspace receives its own port range, process group, Docker lifecycle, browser profile, and diagnostics stream.

## Switching workspaces

Switching workspaces should restore the relevant running applications and browser state without forcing developers or AI agents to recreate tabs, reauthenticate, or restart unrelated services.

## Connect and login

Connect with the Server URL first. Saved servers stay on the client; only one is active. Switching servers drops that client's local workspace state. Remote servers must use HTTPS. Loopback HTTP remains for local development.

<DocSurface mobile>After connect, the sidebar lists workspaces for the active Server. There is no Workspaces tab. Each workspace has a bottom bar with Apps, Git, and Agents.</DocSurface>

### CLI workspace commands

`start` searches the current directory and its parents for `agent-up.json`, then pushes the workspace and application definitions to the Server. The directory containing `agent-up.json` is the workspace root. Running `start` again from the same workspace updates the existing workspace in place.

```bash
agent-up stop
agent-up list
agent-up status
agent-up clear
```

`clear` stops and removes all workspaces currently known to the Server.

`auth` authenticates the CLI with a Server that requires the admin password. Tokens are stored locally per server URL. `auth` commands always require an explicit `--server` argument. They do not fall back to repository `.env` values or `AGENTUP_SERVER_URL`.

```bash
agent-up auth login --server http://localhost:5001 --password "$AGENTUP_ADMIN_PASSWORD"
```

Omit `--password` to enter the admin password interactively. Use `auth status` to check whether authentication is required, and `auth logout` to remove the stored token.

When invoking with `dotnet run`, pass CLI arguments after `--`. The server URL defaults to `$AGENTUP_SERVER_URL` or `http://localhost:5000` when neither is set. When running Server from the repository launch profile, use `http://localhost:5001`.

```bash
dotnet run --project AgentUp.CLI -- start --server http://localhost:5001
```
