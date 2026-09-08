---
title: Server
---

# AgentUp.Server

`AgentUp.Server` owns all runtime state and performs all orchestration.

## Responsibilities

The Server manages:

- Workspace registry.
- Managed source clones.
- Git working-tree review and commits.
- Process lifecycle.
- Port allocation.
- Docker lifecycle.
- Browser lifecycle.
- Browser profiles.
- Browser session persistence.
- Event recording.
- Diagnostics.
- Health monitoring.
- Playwright generation.
- MCP server.
- REST API.

## Single Source of Truth

Desktop, CLI, and MCP clients connect to the Server. They do not keep authoritative copies of workspace state.

## Service Hosting

Packaged installations run `AgentUp.Server` as the local `agent-up-server` service:

- macOS uses launchd.
- Windows uses Windows Service hosting.
- Ubuntu uses systemd.
- NixOS uses a systemd service definition through a Nix module.

Packaged services bind to `http://127.0.0.1:5000` by default. Service definitions that automatically restart the Server must throttle restart attempts to at least 5 seconds so a bind failure, such as another process already using port 5000, cannot create a tight restart loop.

The REST API permits cross-origin browser requests from any HTTP or HTTPS
origin, so the Mobile web/PWA client can reach a Server the user points it at
regardless of where that client is hosted (a local dev port, an installed
PWA, or a deployed preview build). The browser's own mixed-content policy
still applies: a client served over HTTPS cannot fetch a plain-HTTP Server
unless that Server is loopback-hosted, so a remote Server should be reachable
over HTTPS.

This service shape is packaging and lifecycle behavior only. Runtime ownership remains unchanged: all orchestration stays in `AgentUp.Server`, and Desktop stays a client.

This rule keeps concurrent agents, human developers, and automation clients aligned around the same running environment.

## Orchestration Rule

If a feature starts, stops, restarts, navigates, records, allocates, diagnoses, or exports workspace behavior, that logic belongs in the Server.

## Process Environment

When the Server launches a local application process, it injects the workspace's full allocated port map into the process environment. This lets sibling applications discover each other through declared variables such as `WEB_PORT`, `API_PORT`, and `POSTGRES_PORT` without coupling application source code to Agent-Up APIs.

Local application commands are parsed as an executable plus arguments and launched with `ProcessStartInfo.ArgumentList`. The Server rejects shell expressions such as pipes, redirects, variable expansion, command chaining, and subshells before process start.

Managed local application processes also receive `AGENT_UP_AUDIT_ENDPOINT`,
`AGENT_UP_WORKSPACE_ID`, and `AGENT_UP_APPLICATION`. Browser builds can expose
these values to `@agent-up/audit`. The audit endpoint is derived from the
orchestrating Server's configured public/listen URL, including the development
port, rather than assuming the packaged port. The Server accepts frontend events into its
existing audit store and provides bounded, cursor-paginated queries scoped to a
workspace and application.

Process output storage must not use workspace IDs or application names as raw path segments. Repositories that persist process logs must encode or canonicalize those identifiers and verify the resolved path stays under the Server-owned output root before reading, writing, or deleting files.

## Managed Source Clones

The `SourceClones` slice owns repositories Agent-Up clones for itself, as opposed to worktrees registered from the CLI or MCP. `POST /api/source-clones` takes a repository and a branch, validates both, clones into the source clones root, and registers the resulting workspace through the `Workspaces` controller boundary. `GET /api/source-clones/root` reports the configured root.

The root comes from `AGENTUP_SOURCE_CLONES_ROOT` and otherwise defaults to a `sources` directory under the Server data directory. The slice resolves a destination directory from the repository name, verifies it stays under that root, and refuses a clone when the destination already exists.

Remotes are restricted to `http`, `https`, `ssh`, and `git` URLs plus the `user@host:path` form. Local `file://` and transport-helper remotes are rejected so a REST caller cannot make the Server read arbitrary local repositories. Branch names are validated against Git ref rules before any process starts, and the clone runs with `GIT_TERMINAL_PROMPT=0` so a credential prompt cannot hang the Server.

Registration prefers the repository's own `agent-up.json` through the `Orchestration` registration controller. A repository without that file still registers, using the clone directory name and the identity read from the new checkout.

## Git Working Tree

The `Git` slice is the Server-side capability behind the Desktop Git panel and the Mobile Git tab. It resolves the selected workspace's worktree path and exposes three routes:

- `GET /api/workspaces/{workspaceId}/git/changes` returns the uncommitted changes as a directory tree with per-file status.
- `GET /api/workspaces/{workspaceId}/git/file?path=` returns one file's diff, including untracked files.
- `POST /api/workspaces/{workspaceId}/git/commit` stages and commits only the requested paths with the supplied message and returns the new commit.

The provider runs Git through an allowlisted operation set with `ProcessStartInfo.ArgumentList`, rejects pathspec magic, option-shaped paths, and paths that resolve outside the repository root, and always passes `--` before user-supplied paths. Because the commit passes explicit pathspecs, changes to files the caller did not select stay in the worktree.

This slice is separate from the `Commits` slice. `Commits` owns the agent-facing commit queue, which stages vertical slices for a developer to review. `Git` owns the human review-and-commit surface in Desktop and Mobile.

## Tutorial Cleanup

`POST /api/workspaces/tutorial/cleanup` is a Desktop onboarding support endpoint. It stops and removes every registered workspace when the first-run tutorial starts, so stale workspace state cannot render behind onboarding or affect the guided sample setup.
