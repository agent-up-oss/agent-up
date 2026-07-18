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

## No Hardcoded Ports

Applications reference environment variables supplied by Agent-Up. Applications must not assume fixed localhost ports.

The Server owns port allocation and injects the workspace's full allocated port map into each launched local process. Applications should read the relevant environment variables instead of assuming fixed localhost ports.

## No Framework Knowledge

Capability sections such as `dotnet` and `docker` are framework-aware at the ecosystem boundary: Agent-Up can discover installed versions, compare them with declared requirements, and report version mismatch errors before launch.

The legacy `applications` list remains available for opaque commands, and legacy Docker `services` remain available for compatibility. Agent-Up runs legacy application commands through the host platform shell: `cmd.exe /C` on Windows and Bash on Unix-like systems. Use command syntax that is valid on the operating systems where the workspace will run.
