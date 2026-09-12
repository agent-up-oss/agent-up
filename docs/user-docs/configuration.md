---
title: Configuration
---

# Configuration

Every repository root contains an `agent-up.json` file.

Configuration is declarative. Capability-aware sections describe ecosystem requirements, launch inputs, and port variables. Legacy applications can still describe opaque shell commands.

```json
{
  "name": "Inventory",
  "dotnet": [
    {
      "name": "API",
      "sdk": "10.0.x",
      "run": {
        "project": "src/Api/Api.csproj",
        "arguments": ["--no-launch-profile"]
      },
      "environmentFiles": [".env"],
      "environment": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "ports": [{ "variable": "API_PORT", "defaultPort": 5000 }]
    }
  ],
  "docker": [
    {
      "name": "Database",
      "image": "postgres:17",
      "environmentFiles": [".env.database"],
      "ports": [{ "variable": "DB_PORT", "defaultPort": 5432 }]
    }
  ]
}
```

Legacy executable commands remain supported when Agent-Up should only launch the process and inject ports:

```json
{
  "name": "Inventory",
  "applications": [
    {
      "name": "Frontend",
      "command": "dotnet run --project src/Web",
      "path": "/",
      "environmentFiles": [".env"],
      "environment": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "ports": [{ "variable": "WEB_PORT", "defaultPort": 5100 }]
    }
  ],
  "services": [
    {
      "name": "Database",
      "image": "postgres:16",
      "ports": [{ "variable": "POSTGRES_PORT", "defaultPort": 5432, "protocol": "tcp" }],
      "environment": {
        "POSTGRES_PASSWORD": "not-a-real-value"
      },
      "volumes": ["pgdata:/var/lib/postgresql/data"]
    }
  ]
}
```

## No Hardcoded Ports

Applications reference environment variables supplied by Agent-Up. Applications must not assume fixed localhost ports.

The Server owns port allocation and injects the workspace's full allocated port map into each launched local process. Applications should read the relevant environment variables instead of assuming fixed localhost ports.

Docker containers can call host-run applications in the same workspace through `host.agent-up` when the host-run application listens on an address reachable from Docker's host gateway. A process bound only to `127.0.0.1` is not reachable from containers through this alias. Inline Docker environment values may reference allocated workspace ports with `${VARIABLE}`, such as `BACKEND_URL=http://host.agent-up:${API_PORT}`.

## No Framework Knowledge

Capability sections such as `dotnet` and `docker` are framework-aware at the ecosystem boundary: Agent-Up can discover installed versions, compare them with declared requirements, and report version mismatch errors before launch.

The legacy `applications` list remains available for executable-plus-arguments commands, and legacy Docker `services` remain available for compatibility. Agent-Up launches local application commands directly with an argument list; shell expressions such as pipes, redirects, variable expansion, command chaining, and subshells are rejected. Put that behavior in a checked-in script and call the script through an allowed runtime command when needed.

## Environment And Secrets

Applications, capability-backed applications, Docker capabilities, and legacy Docker services can declare `environmentFiles` for `.env`-style secrets and `environment` for inline non-secret values. Environment file paths are relative to the workspace root. File values are loaded when the Server launches the process and are not copied into saved workspace state; inline `environment` values are part of the workspace definition.

Agent-Up applies local process environment values in this order: environment files, inline `environment`, then Server-allocated port variables. For Docker containers, Agent-Up passes environment files to Docker, interpolates `${VARIABLE}` references in inline `environment` values from the allocated workspace port map, and passes the resolved values as Docker `-e` arguments.

## Workspace agent CLIs

Workspace agent chat uses first-party Codex, Cursor, and Claude capability
adapters on the Server. Those adapters launch only the ACP command declared in
Agent-Up capability inventory, not a hardcoded executable name. Server and
Desktop installments share the same inventory files, which are merged by
capability id. Lookup order is `AGENTUP_CAPABILITY_INVENTORY_PATH` when set,
then `/etc/agent-up/capabilities.json`, then a user overlay at
`~/.config/agent-up/capabilities.local.json`, then
`~/.config/agent-up/capabilities.json`, then
`.agent-up-dev/capabilities.json` walking up from the Server's working
directory. Earlier files win for a field; later files fill unspecified
fields, so the overlay takes precedence over the user installment file. A Home Manager version list therefore does not hide ACP `command`
entries from the overlay or a local `nix-shell` inventory. A rooted `command`
is how an installment points at a version on disk; `versions` records which
versions that installment has enabled. Example:

```json
[
  { "id": "dotnet", "versions": ["10.0.x"] },
  { "id": "codex", "versions": ["dev"], "command": "/opt/agent-up/codex-acp", "arguments": [] },
  { "id": "cursor", "versions": ["dev"], "command": "agent", "arguments": ["acp"] },
  { "id": "claude", "versions": ["dev"], "command": "claude-agent-acp", "arguments": [] }
]
```

Discovery then looks for that declared command on `PATH` and in well-known
install locations such as `~/.local/bin`. The interactive `codex` and `claude`
CLIs are not ACP servers, and the Cursor IDE is not the Cursor Agent CLI.
Sign in with the corresponding CLI first; Agent-Up reuses the CLI's supported
local subscription login and does not ask for or store an API token. An
unavailable executable is disabled in the Desktop and Mobile agent picker.

Server operators can still override a command or its arguments in
`appsettings.json` under `Agents:Codex`, `Agents:Cursor`, or `Agents:Claude`.
Services may have a different `PATH` from an interactive terminal, so use an
absolute command path when the installed service cannot discover an adapter.
