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
| `display` | [Display](#display-object) | No | derived workspace visuals | Optional Desktop-only workspace list label overrides. |
| `applications` | array of [Application](#application-object) | No | `[]` | Legacy local process applications launched directly from executable-plus-arguments commands. |
| `services` | array of [Docker Service](#docker-service-object) | No | `[]` | Legacy Docker service definitions. |
| `dotnet` | array of [.NET Application](#net-application-object) | No | `[]` | .NET applications launched through the Agent-Up .NET capability. |
| `docker` | array of [Docker Capability](#docker-capability-object) | No | `[]` | Docker containers launched through the Agent-Up Docker capability. |
| `prompts` | [Prompts](#prompts-object) | No | default Agent-Up guidance | Optional repository-specific guidance for AI agents. |
| `commits` | [Commits](#commits-object) | No | no build/test enforcement | Optional build and test commands the commit queue resolves and attaches to queued entries. |

## Display Object

Used at the root to make Desktop workspace list entries more expressive. These values only affect Desktop visuals; repository path, worktree path, Git branch detection, commit identity, audit identity, and process working directories still come from the real workspace.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `name` | string | No | root `name` | Workspace list title override. |
| `branch` | string | No | detected Git branch or `not on a git branch` | Workspace list subtitle override. |

## Prompts Object

Used at the root to refine default AI-agent behavior for this repository.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `commitPolicy` | string | No | Agent-Up default commit policy | Repository-specific commit guidance. Agents should use it when choosing commit prefixes, scopes, and queue grouping. |

Default commit policy: scope commit messages to the queued slice; use `feat` for user-facing additions, `fix` for user-facing fixes, `test` for test-only or smoke-validation changes, `refactor` for no-behavior source changes, `chore` for maintenance, packaging, CI, or tooling with no customer runtime effect, `style` for CSS/HTML-only changes, and `docs` for documentation-only changes.

## Commits Object

Used at the root to declare build and test commands the local commit queue (`agentup commits enqueue` and the equivalent Server MCP `enqueue_commit` tool) resolves for a queued entry's changed files. When present, resolved commands are merged into the entry's `tests`, alongside anything passed explicitly with `--tests`. Omitting `commits`, or any of its properties, keeps the previous behavior: no commands are resolved automatically.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `build` | array of strings | No | `[]` | General commands that apply to every queued entry, such as building the solution. |
| `test` | array of strings | No | `[]` | General test commands that apply to every queued entry, such as an architecture test suite. |
| `projects` | object of [Commits Project](#commits-project-object) | No | `{}` | Per-project test commands, keyed by the top-level project directory a changed file falls under (the first path segment). |

## Commits Project Object

Used as values in `commits.projects`, keyed by project directory name.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `test` | array of strings | No | `[]` | Test commands for this project. Resolved for a queued entry whenever one of its files falls under this project, or under a project that depends on it (see `dependsOn`). |
| `dependsOn` | array of strings | No | `[]` | Other project keys this project depends on. Used to find transitive dependents: when a dependency's files change, this project's `test` commands are resolved too, and so on transitively. |

Example:

```json
{
  "commits": {
    "build": ["dotnet build agent-up.sln"],
    "test": ["dotnet test AgentUp.Architecture.Tests"],
    "projects": {
      "AgentUp.CommitPolicy": {
        "test": ["dotnet test AgentUp.CommitPolicy.Tests"]
      },
      "AgentUp.Server": {
        "test": ["dotnet test AgentUp.Server.Tests"],
        "dependsOn": ["AgentUp.CommitPolicy"]
      },
      "AgentUp.Mobile": {
        "test": []
      }
    }
  }
}
```

With this configuration, an entry touching only `AgentUp.CommitPolicy/...` files resolves the general `build` and `test` commands plus `AgentUp.CommitPolicy`'s own tests and `AgentUp.Server`'s tests, because `AgentUp.Server` depends on `AgentUp.CommitPolicy`. An entry touching only documentation files resolves just the general `build` and `test` commands. `AgentUp.Mobile` is declared with no test commands yet, ready for a test command to be added once mobile tests exist, without any change to the resolution logic.

## Application Object

Used in `applications`.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `name` | string | Yes | none | Application display name. Names are used in application lists and start/stop/restart operations. |
| `command` | string | Yes | none | Executable-plus-arguments command used to start the application. Agent-Up launches it directly from the configured `path`, rejects shell expressions, and requires the first token to be an allowlisted executable name such as `node`, `npm`, `dotnet`, `python`, `cargo`, `go`, `java`, `make`, `mvn`, `gradle`, `bun`, `pnpm`, or `yarn`. |
| `install` | string or null | No | none | Executable-plus-arguments command run to completion in the same `path`, before every start of `command` — subject to the same executable allowlist and shell-expression rejection as `command`. Runs unconditionally on every `agent-up start` (and every restart), so it must be safe to re-run, like `npm install`, `dotnet restore`, or `pip install -r requirements.txt`; the Server does not track whether it "already ran". Output is streamed to the application console prefixed with `[install]`. A non-zero exit fails the start and the application is not launched. |
| `path` | string or null | No | workspace root | Working directory for the command, relative to the workspace root. Use `null` or omit it to run from the workspace root. |
| `ports` | array of [Port](#port-object) | No | `[]` | Port declarations owned and allocated by the Server. |
| `environment` | object of string values | No | `{}` | Inline environment variables for this process. Use for values safe to store in `agent-up.json` and Server workspace state. |
| `environmentFiles` | array of strings | No | `[]` | Workspace-relative `.env`-style files loaded when the process starts. |

Example:

```json
{
  "name": "Web",
  "command": "npm run dev",
  "install": "npm install",
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
| `command` | array of strings | No | `null` | Arguments appended after the image in `docker run`, overriding the container's default command (equivalent to Docker Compose's `command`). |

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
| `command` | array of strings | No | `null` | Arguments appended after the image in `docker run`, overriding the container's default command (equivalent to Docker Compose's `command`). |

## Port Object

Used in `applications[].ports`, `dotnet[].ports`, `docker[].ports`, and `services[].ports`.

| Property | Type | Required | Default | Description |
|---|---:|---:|---:|---|
| `variable` | string or null | No | `null` | Environment variable that receives the Server-allocated host port. Omit or set to `null` when no environment variable should be injected. |
| `defaultPort` | integer | Yes | none | Preferred or conventional port for the application/container. Agent-Up uses this as allocation intent and container target port for Docker mappings. |
| `protocol` | string | No | `http` | Protocol label for the port, usually `http` or `tcp`. |
| `healthCheck` | string or null | No | `null` | HTTP path the Server probes every 5 seconds to determine port health (e.g. `"/health"`). When set, the port LED shows amber (Checking) until the path responds with a non-5xx status, then green (Healthy); red (Unhealthy) if it later fails. Omitting this field falls back to a client-side TCP probe. |
| `metrics` | string or null | No | `null` | HTTP path the Server pulls every 30 seconds for application metrics (e.g. `"/metrics"`). The endpoint should return JSON with a `metrics` object or top-level string/number fields. Results are recorded in the application audit trail; applications do not need Agent-Up dependencies. |

For local processes, every application receives the full workspace port map. For Docker containers, Agent-Up publishes each declared port as `allocatedHostPort:defaultPort` and adds the `host.agent-up` hostname for reaching host-run workspace applications from inside the container when those applications listen on an address reachable from Docker's host gateway. Loopback-only host listeners are not reachable from containers through this alias.

## Environment Object

Used as `environment` on all launchable entries.

| Constraint | Description |
|---|---|
| Type | JSON object. |
| Keys | Environment variable names. |
| Values | Strings. |
| Storage | Stored in `agent-up.json` and Server workspace state. |
| Intended use | Non-secret values, feature flags, and development mode switches. |

Docker container environment values can reference workspace port variables with `${VARIABLE}`. Agent-Up resolves those references from the workspace-wide allocated port map before starting the container.

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
