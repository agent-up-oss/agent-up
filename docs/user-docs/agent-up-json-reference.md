---
title: Reference
---

# agent-up.json Reference

This page documents the JSON contract for `agent-up.json`.

Property names are shown in the JSON form Agent-Up examples use. Existing configurations that omit newly added optional properties remain valid.

## Root Object

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `name` | string | Yes | none | Human-readable project name shown for the workspace. |
| `applications` | array of [Application](#application-object) | No | `[]` | Legacy local process applications launched from opaque shell commands. |
| `services` | array of [Docker Service](#docker-service-object) | No | `[]` | Legacy Docker service definitions. |
| `dotnet` | array of [.NET Application](#net-application-object) | No | `[]` | .NET applications launched through the Agent-Up .NET capability. |
| `docker` | array of [Docker Capability](#docker-capability-object) | No | `[]` | Docker containers launched through the Agent-Up Docker capability. |

## Application Object

Used in `applications`.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `name` | string | Yes | none | Application display name. Names are used in application lists and start/stop/restart operations. |
| `command` | string | Yes | none | Shell command used to start the application. Agent-Up runs it through `cmd.exe /C` on Windows and `bash -c` on Unix-like systems. |
| `path` | string or null | No | workspace root | Working directory for the command, relative to the workspace root. Use `null` or omit it to run from the workspace root. |
| `ports` | array of [Port](#port-object) | No | `[]` | Port declarations owned and allocated by the Server. |
| `environment` | object of string values | No | `{}` | Inline environment variables for this process. Use for values safe to store in `agent-up.json` and Server workspace state. |
| `environmentFiles` | array of strings | No | `[]` | Workspace-relative `.env`-style files loaded when the process starts. |

Example:

```json
{
  "name": "Web",
  "command": "npm run dev",
  "path": "web",
  "environmentFiles": [".env"],
  "environment": {
    "NODE_ENV": "development"
  },
  "ports": [
    { "variable": "WEB_PORT", "defaultPort": 5173, "protocol": "http" }
  ]
}
```

## .NET Application Object

Used in `dotnet`.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `name` | string | Yes | none | Application display name. |
| `sdk` | string or null | No | no version requirement | Required .NET SDK version expression, such as `10.0.x`. The .NET capability validates this before launch. |
| `run` | [.NET Run](#net-run-object) | Yes | none | `dotnet run` launch inputs. |
| `ports` | array of [Port](#port-object) | No | `[]` | Port declarations owned and allocated by the Server. |
| `environment` | object of string values | No | `{}` | Inline environment variables for this process. |
| `environmentFiles` | array of strings | No | `[]` | Workspace-relative `.env`-style files loaded when the process starts. |

Example:

```json
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
  "ports": [
    { "variable": "API_PORT", "defaultPort": 5000, "protocol": "http" }
  ]
}
```

## .NET Run Object

Used as `dotnet[].run`.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `project` | string | Yes | none | Project path passed to `dotnet run --project`. |
| `arguments` | array of strings | No | `[]` | Additional command arguments passed to `dotnet run`. |

## Docker Capability Object

Used in `docker`.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `name` | string | Yes | none | Container display name. |
| `image` | string | Yes | none | Docker image reference. |
| `ports` | array of [Port](#port-object) | No | `[]` | Container port declarations owned and allocated by the Server. |
| `environment` | object of string values | No | `{}` | Inline container environment variables passed as Docker `-e` arguments. |
| `volumes` | array of strings | No | `[]` | Docker volume mappings passed as Docker `-v` arguments. |
| `environmentFiles` | array of strings | No | `[]` | Workspace-relative files passed to Docker as `--env-file` arguments. |

Example:

```json
{
  "name": "Database",
  "image": "postgres:17",
  "environmentFiles": [".env.database"],
  "environment": {
    "POSTGRES_USER": "inventory"
  },
  "volumes": ["inventory-pgdata:/var/lib/postgresql/data"],
  "ports": [
    { "variable": "DB_PORT", "defaultPort": 5432, "protocol": "tcp" }
  ]
}
```

## Docker Service Object

Used in `services`. This is the legacy Docker service shape. It remains supported for compatibility.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `name` | string | Yes | none | Service display name. |
| `image` | string | Yes | none | Docker image reference. |
| `ports` | array of [Port](#port-object) | No | `[]` | Container port declarations owned and allocated by the Server. |
| `environment` | object of string values | No | `{}` | Inline container environment variables passed as Docker `-e` arguments. |
| `volumes` | array of strings | No | `[]` | Docker volume mappings passed as Docker `-v` arguments. |
| `environmentFiles` | array of strings | No | `[]` | Workspace-relative files passed to Docker as `--env-file` arguments. |

## Port Object

Used in `applications[].ports`, `dotnet[].ports`, `docker[].ports`, and `services[].ports`.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `variable` | string or null | No | `null` | Environment variable that receives the Server-allocated host port. Omit or set to `null` when no environment variable should be injected. |
| `defaultPort` | integer | Yes | none | Preferred or conventional port for the application/container. Agent-Up uses this as allocation intent and container target port for Docker mappings. |
| `protocol` | string | No | `http` | Protocol label for the port, usually `http` or `tcp`. |

For local processes, every application receives the full workspace port map. For Docker containers, Agent-Up publishes each declared port as `allocatedHostPort:defaultPort`.

## Environment Object

Used as `environment` on all launchable entries.

| Constraint | Description |
|---|---|
| Type | JSON object. |
| Keys | Environment variable names. |
| Values | Strings. |
| Storage | Stored in `agent-up.json` and Server workspace state. |
| Intended use | Non-secret values, feature flags, and development mode switches. |

Example:

```json
{
  "environment": {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "FEATURE_FLAGS": "search,checkout"
  }
}
```

## Environment Files Array

Used as `environmentFiles` on all launchable entries.

| Constraint | Description |
|---|---|
| Type | Array of strings. |
| Path rules | Each path is relative to the workspace root. Absolute paths and paths that escape the workspace root are rejected. |
| Storage | Only file paths are stored. File values are read at launch time. |
| Missing files | Launch fails if a declared file does not exist. |
| Intended use | Local secrets and developer-specific values. |

See [environment and secrets](./agent-up-json-environment.md) for parsing and precedence.
