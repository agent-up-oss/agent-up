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

## No Framework Knowledge

Capability sections such as `dotnet` and `docker` are framework-aware at the ecosystem boundary: Agent-Up can discover installed versions, compare them with declared requirements, and report version mismatch errors before launch.

The legacy `applications` list remains available for executable-plus-arguments commands, and legacy Docker `services` remain available for compatibility. Agent-Up launches local application commands directly with an argument list; shell expressions such as pipes, redirects, variable expansion, command chaining, and subshells are rejected. Put that behavior in a checked-in script and call the script through an allowed runtime command when needed.

## Environment And Secrets

Applications, capability-backed applications, Docker capabilities, and legacy Docker services can declare `environmentFiles` for `.env`-style secrets and `environment` for inline non-secret values. Environment file paths are relative to the workspace root. File values are loaded when the Server launches the process and are not copied into saved workspace state; inline `environment` values are part of the workspace definition.

Agent-Up applies local process environment values in this order: environment files, inline `environment`, then Server-allocated port variables.
