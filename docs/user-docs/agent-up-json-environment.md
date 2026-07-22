---
title: Environment And Secrets
---

# agent-up.json Environment And Secrets

Agent-Up supports environment injection on every launchable entry:

- `applications[]`
- `dotnet[]`
- `docker[]`
- `services[]`

Use `environment` for values that are safe to store in the JSON file. Use `environmentFiles` for local secrets and developer-specific values.

## Local Process Precedence

For `applications[]` and `dotnet[]`, Agent-Up builds the process environment in this order:

1. The inherited Server process environment.
2. Variables from `environmentFiles`, in listed order.
3. Variables from inline `environment`.
4. Server-allocated port variables.

Later values override earlier values with the same key. Port variables win so applications always receive the actual Server-owned allocation.

## Docker Precedence

For `docker[]` and `services[]`, Agent-Up passes:

- each `environmentFiles` entry as a Docker `--env-file` argument.
- each inline `environment` entry as a Docker `-e KEY=value` argument.

Docker applies its own precedence rules after Agent-Up constructs the command. Inline `environment` values are passed after `--env-file` arguments.

## Environment File Paths

Every `environmentFiles` path must be relative to the workspace root.

Valid:

```json
{
  "environmentFiles": [".env", "config/.env.local"]
}
```

Invalid:

```json
{
  "environmentFiles": ["/etc/secrets/app.env", "../shared/.env"]
}
```

Agent-Up rejects absolute paths and paths that resolve outside the workspace root.

## Environment File Syntax

Environment files support:

- `KEY=value`
- `export KEY=value`
- blank lines
- comments that start with `#`
- matching single or double quotes around the full value

Example:

```text
# local only
DATABASE_PASSWORD=secret
API_TOKEN="token with spaces"
export FEATURE_FLAG=true
```

Agent-Up does not expand variables inside environment files. A value such as `API_URL=http://localhost:${API_PORT}` is passed through literally by Agent-Up.

## Storage

`environment` values are part of the workspace definition. They may be stored by the Server and returned through workspace/application APIs.

`environmentFiles` stores only file paths. File contents are read when the Server launches the application or container.

For secrets, prefer `environmentFiles` and keep those files out of source control.
