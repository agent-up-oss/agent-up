---
title: agent-up.json
---

# agent-up.json

`agent-up.json` describes a repository's development applications and capability requirements.

## Fields

`name` identifies the workspace project.

`applications` lists legacy opaque shell-command applications. This shape remains supported.

`services` lists legacy Docker service definitions. This shape remains supported.

`dotnet` lists .NET applications that should run through the Agent-Up .NET capability.

`docker` lists Docker services that should run through the Agent-Up Docker capability.

## Application Fields

`name` is the display name for the application.

`command` is the shell command used to start a legacy application. Agent-Up runs it through the host platform shell: `cmd.exe /C` on Windows and Bash on Unix-like systems.

`portVariable` is the environment variable that receives the Server-allocated port.

`path` is the browser path to open for that application.

## Capability Fields

Use capability sections when Agent-Up should understand the ecosystem requirement rather than only run an opaque command.

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
      "ports": [{ "variable": "API_PORT", "defaultPort": 5000 }]
    }
  ],
  "docker": [
    {
      "name": "Database",
      "image": "postgres:17",
      "ports": [{ "variable": "DB_PORT", "defaultPort": 5432 }]
    }
  ]
}
```

Capability adapters discover installed versions, compare them with declared requirements, and report mismatch status through the Server so Desktop, CLI, and automation clients can show actionable errors.

## Port Variables

Agent-Up allocates ports per workspace and writes those values into environment variables. Each launched local application receives the full workspace port map, so a frontend can discover the API port and an API can discover infrastructure ports without hardcoding localhost values.

```text
WEB_PORT=5100
API_PORT=5101
AUTH_PORT=5102
```

This keeps application source code free of Agent-Up dependencies.
