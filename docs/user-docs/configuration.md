---
title: Configuration
---

# Configuration

Every repository root contains an `agent-up.json` file.

Configuration is declarative. Applications describe how they should be launched, which environment variable should receive their allocated port, and which browser path should open.

```json
{
  "name": "Inventory",
  "applications": [
    {
      "name": "Frontend",
      "command": "dotnet run --project src/Web",
      "path": "/",
      "ports": [
        { "variable": "WEB_PORT", "defaultPort": 5100 }
      ]
    },
    {
      "name": "API",
      "command": "dotnet run --project src/Api",
      "path": "/swagger",
      "ports": [
        { "variable": "API_PORT", "defaultPort": 5101 }
      ]
    }
  ],
  "services": [
    {
      "name": "Database",
      "image": "postgres:16",
      "ports": [
        { "variable": "POSTGRES_PORT", "defaultPort": 5432, "protocol": "tcp" }
      ],
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

The `command` field is opaque to Agent-Up. It can launch any framework or runtime as long as the application can receive its port through the configured environment variable.

Agent-Up runs local application commands through the host platform shell: `cmd.exe /C` on Windows and Bash on Unix-like systems. Use command syntax that is valid on the operating systems where the workspace will run.
