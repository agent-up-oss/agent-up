---
title: Workspaces
---

<DocEyebrow slice="Workspaces" status="available" />

# Workspaces

<DocWhat>
A workspace is the unit of isolation for a developer or agent session. Start it from the directory that contains `agent-up.json`.

It is identified by project path. It may include Git metadata, a browser profile, Docker infrastructure, running processes, an allocated port range, diagnostics, and event history.

Non-Git paths still work and display as `not on a git branch`. Agent-Up can also clone a repository for you and register the result.
</DocWhat>

<DocSpine>
<DocBeat>Add `agent-up.json` at the repository root</DocBeat>
<DocBeat>Start the workspace</DocBeat>
<DocBeat>Switch among isolated sessions</DocBeat>
</DocSpine>

<DocContract label="Command">agent-up start</DocContract>

<DocCallout kind="warning">
<DocFact label="Packaged">{'http://localhost:5000'}</DocFact>
<DocFact label="Repository">{'http://localhost:5001'}</DocFact>
</DocCallout>

<DocSurfaces>
<DocSurface cli>From the directory that contains `agent-up.json`.</DocSurface>
<DocSurface desktop>The workspace list `+` control clones a repository at a branch and registers it.</DocSurface>
<DocSurface mobile>The same `+` control lives on the sidebar workspace list. There is no Workspaces tab.</DocSurface>
</DocSurfaces>

## Worktree model

The workspace identity is the project path. When that path is a Git repository or worktree, Agent-Up records the repository root, branch, and commit. When no Git repository exists, the workspace still works and displays `not on a git branch`.

Git worktrees are the recommended model for AI agents working in the same repository because they preserve separate branches, running applications, browser sessions, and validation state.

<DocSurfaces>
<DocSurface desktop>The Overview tab has a branch dropdown for local and remote-tracking branches, Fetch/Pull/Push, plus workspace identity and Server-owned CPU, memory, storage, and process totals.</DocSurface>
<DocSurface mobile>Those Git controls live on the workspace Git tab, beside Apps and Agents.</DocSurface>
</DocSurfaces>

## Managed source clones

Agent-Up can manage the clone itself instead of only registering a checkout you created.

<DocSteps>
<DocStep title="Ask for a repository and branch">
Both the Desktop workspace list and the Mobile sidebar have a `+` button.
</DocStep>
<DocStep title="Clone into the source clones directory">
The result is registered so it appears in the workspace list on every connected client.
</DocStep>
<DocStep title="Keep isolation">
Clones created this way behave like any other workspace: they get their own port range, processes, browser profile, and diagnostics.
</DocStep>
</DocSteps>

The repository must be an `http`, `https`, `ssh`, or `git` remote URL, or the `user@host:path` form. The branch must already exist on the remote. Agent-Up refuses a clone when a directory with the repository's name already exists under the source clones directory.

The source clones directory defaults to a `sources` folder inside the Server data directory. Set `AGENTUP_SOURCE_CLONES_ROOT` in the Server environment to keep managed clones somewhere else, such as a larger disk.

Removing such a workspace unregisters it from Agent-Up. It does not delete the clone from disk.

## Runtime isolation

Workspace isolation prevents collisions between concurrent development sessions.

<DocFacts label="Each workspace owns">
<DocFact label="Ports">Its own contiguous port range</DocFact>
<DocFact label="Processes">Its own process group</DocFact>
<DocFact label="Docker">Its own Docker lifecycle</DocFact>
<DocFact label="Browser">Its own browser profile</DocFact>
<DocFact label="Diagnostics">Its own diagnostics stream</DocFact>
</DocFacts>

## Switching workspaces

Switching workspaces should restore the relevant running applications and browser state without forcing developers or AI agents to recreate tabs, reauthenticate, or restart unrelated services.

## Connect and login

<DocSteps>
<DocStep title="Open Demo to try the product">
Desktop and Mobile always list a built-in Demo server. It is sample state inside the client, not a running Agent-Up Server.
</DocStep>
<DocStep title="Connect with the Server URL">
Saved servers stay on the client; only one is active.
</DocStep>
<DocStep title="Use HTTPS off loopback">
Remote servers must use HTTPS. Loopback HTTP remains for local development.
</DocStep>
<DocStep title="Expect a clean client switch">
Switching servers drops that client's local workspace state.
</DocStep>
</DocSteps>

<DocSurfaces>
<DocSurface mobile>After connect, the sidebar lists workspaces for the signed-in Server and that Server URL with Logout. Each workspace has a bottom bar with Apps, Git, Agents, and Settings. Settings lists capability modules. The Apps tab start/stop control requests the Server-owned lifecycle and shows live health.</DocSurface>
</DocSurfaces>

## CLI workspace commands

`start` searches the current directory and its parents for `agent-up.json`, then pushes the workspace definition to the Server: identity, legacy `applications` / `desktopApplications` / `services`, and every other root array as a runtime section. The Server binds those arrays to enabled runtime-kind modules. The directory containing `agent-up.json` is the workspace root. Running `start` again from the same workspace updates the existing workspace in place.

```bash
agent-up start --server http://localhost:5001
```

<DocFacts label="Other commands">
<DocFact label="stop">Stops the current workspace</DocFact>
<DocFact label="list">Lists workspaces the Server knows</DocFact>
<DocFact label="status">Shows workspace status</DocFact>
<DocFact label="clear">Stops and removes all known workspaces</DocFact>
</DocFacts>

`auth` authenticates the CLI with a Server that requires the admin password. Tokens are stored locally per server URL. `auth` commands always require an explicit `--server` argument. They do not fall back to repository `.env` values or `AGENTUP_SERVER_URL`.

```bash
agent-up auth login --server http://localhost:5001 --password "$AGENTUP_ADMIN_PASSWORD"
```

Omit `--password` to enter the admin password interactively. Use `auth status` to check whether authentication is required, and `auth logout` to remove the stored token.

When invoking with `dotnet run`, pass CLI arguments after `--`. Use `http://localhost:5001` when running Server from the repository launch profile.

```bash
dotnet run --project AgentUp.CLI -- start --server http://localhost:5001
```

<DocNext href="/docs/applications" title="Applications">
Processes that run inside a workspace.
</DocNext>
