---
title: Server
---

# AgentUp.Server

`AgentUp.Server` owns all runtime state and performs all orchestration.

## Responsibilities

The Server manages:

- Workspace registry.
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

This service shape is packaging and lifecycle behavior only. Runtime ownership remains unchanged: all orchestration stays in `AgentUp.Server`, and Desktop stays a client.

This rule keeps concurrent agents, human developers, and automation clients aligned around the same running environment.

## Orchestration Rule

If a feature starts, stops, restarts, navigates, records, allocates, diagnoses, or exports workspace behavior, that logic belongs in the Server.

## Process Environment

When the Server launches a local application process, it injects the workspace's full allocated port map into the process environment. This lets sibling applications discover each other through declared variables such as `WEB_PORT`, `API_PORT`, and `POSTGRES_PORT` without coupling application source code to Agent-Up APIs.

Local application commands are executed through the host platform shell: `cmd.exe /C` on Windows and Bash on Unix-like systems. Command strings in `agent-up.json` should therefore use syntax that is valid for the operating systems where that workspace is expected to run.

Process output storage must not use workspace IDs or application names as raw path segments. Repositories that persist process logs must encode or canonicalize those identifiers and verify the resolved path stays under the Server-owned output root before reading, writing, or deleting files.

## Tutorial Cleanup

`POST /api/workspaces/tutorial/cleanup` is a Desktop onboarding support endpoint. It stops and removes every registered workspace when the first-run tutorial starts, so stale workspace state cannot render behind onboarding or affect the guided sample setup.
