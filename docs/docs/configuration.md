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
      "portVariable": "WEB_PORT",
      "path": "/"
    },
    {
      "name": "API",
      "command": "dotnet run --project src/Api",
      "portVariable": "API_PORT",
      "path": "/swagger"
    }
  ],
  "docker": [
    "docker compose up -d"
  ]
}
```

## No Hardcoded Ports

Applications reference environment variables supplied by Agent-Up. Applications must not assume fixed localhost ports.

The Server owns port allocation and injects values into each launched process.

## No Framework Knowledge

The `command` field is opaque to Agent-Up. It can launch any framework or runtime as long as the application can receive its port through the configured environment variable.
