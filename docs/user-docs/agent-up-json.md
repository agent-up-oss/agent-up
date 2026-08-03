---
title: Overview
---

# agent-up.json

`agent-up.json` is the repository-level contract Agent-Up reads when a workspace is started.

The file declares the development applications, local services, capability requirements, ports, and launch-time environment that the Server should own for the workspace. Applications do not reference Agent-Up packages, SDKs, or APIs; Agent-Up supplies runtime values through process launch configuration.

## File Location

Place `agent-up.json` at the repository or worktree root. The CLI reads this file from the current working directory when `agent-up start` runs and sends the definition to the Server.

## Top-Level Shape

```json
{
  "name": "Inventory",
  "display": {},
  "applications": [],
  "services": [],
  "dotnet": [],
  "docker": [],
  "prompts": {}
}
```

Only `name` is required. All application and service arrays are optional.

Use optional `display` values to override the Desktop workspace list title and subtitle without changing repository, worktree, or Git identity handling. `display.name` replaces the visible workspace title, and `display.branch` replaces the visible branch subtitle.

Use optional `prompts` values, such as `prompts.commitPolicy`, to give AI agents repository-specific guidance without changing application runtime configuration.

## Preferred Sections

Use capability-aware sections when Agent-Up should understand the ecosystem boundary:

- `dotnet` for .NET applications launched through the .NET capability.
- `docker` for Docker containers launched through the Docker capability.

Use compatibility sections when Agent-Up should preserve a legacy executable-plus-arguments command or Docker service shape:

- `applications` for local executable-plus-arguments applications.
- `services` for legacy Docker services.

The complete field contract is in the [reference](./agent-up-json-reference.md).

## Environment And Secrets

Every launchable section supports:

- `environment` for inline non-secret values.
- `environmentFiles` for `.env`-style files such as `.env`, `.env.local`, or `.env.database`.

See [environment and secrets](./agent-up-json-environment.md) for precedence, parsing, storage, and Docker behavior.

## Examples

See [examples](./agent-up-json-examples.md) for complete capability-aware and legacy configuration files.
