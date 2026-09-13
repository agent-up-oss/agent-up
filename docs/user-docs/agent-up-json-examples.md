---
title: Examples
---

# agent-up.json Examples

## Capability-Aware .NET And Docker

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
      "ports": [
        { "variable": "API_PORT", "defaultPort": 5000, "protocol": "http" }
      ]
    }
  ],
  "docker": [
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
  ]
}
```

## Docker Container With A Custom Command

Use `command` to override a container's default entrypoint arguments, for example to run Redpanda in dev-container mode:

```json
{
  "name": "Inventory",
  "docker": [
    {
      "name": "Redpanda",
      "image": "docker.redpanda.com/redpandadata/redpanda:v24.2.4",
      "command": [
        "redpanda",
        "start",
        "--mode",
        "dev-container",
        "--smp",
        "1",
        "--default-log-level=warn"
      ],
      "ports": [
        { "variable": "KAFKA_PORT", "defaultPort": 19092, "protocol": "tcp" }
      ]
    }
  ]
}
```

## Legacy Local Commands

```json
{
  "name": "Inventory",
  "applications": [
    {
      "name": "Frontend",
      "command": "npm run dev",
      "path": "web",
      "environmentFiles": [".env"],
      "environment": {
        "NODE_ENV": "development"
      },
      "ports": [
        { "variable": "WEB_PORT", "defaultPort": 5173, "protocol": "http" }
      ]
    },
    {
      "name": "API",
      "command": "./gradlew bootRun",
      "path": "api",
      "environmentFiles": [".env"],
      "environment": {
        "DATABASE_URL": "localhost",
        "SPRING_DATASOURCE_EXTERNAL": "true",
        "KEY_SERVICE_EXTERNAL": "true"
      },
      "ports": [
        { "variable": "SERVER_PORT", "defaultPort": 8080, "protocol": "http" }
      ]
    }
  ]
}
```

## Linux Desktop Application

Linux GUI processes belong in `desktopApplications`. Agent-Up starts each one on a Server-owned Xvfb display and streams the framebuffer to Desktop and Mobile.

This repository's `agent-up.json` includes `Examples/linux-desktop`, an Avalonia window used to exercise that path:

```json
{
  "name": "Inventory",
  "desktopApplications": [
    {
      "name": "Sample Desktop",
      "command": "dotnet run --project Examples/linux-desktop/LinuxDesktop.csproj --no-launch-profile",
      "install": "dotnet build Examples/linux-desktop/LinuxDesktop.csproj --nologo --no-incremental",
      "path": ".",
      "runtime": "linux",
      "window": { "width": 800, "height": 600 }
    }
  ]
}
```

Desktop applications currently require a Linux Server host. On macOS and Windows the Server leaves the desktop entry failed and still starts the rest of the workspace.

## Legacy Docker Service

```json
{
  "name": "Inventory",
  "services": [
    {
      "name": "Database",
      "image": "postgres:16",
      "environmentFiles": [".env.database"],
      "environment": {
        "POSTGRES_USER": "inventory"
      },
      "volumes": ["inventory-pgdata:/var/lib/postgresql/data"],
      "ports": [
        { "variable": "POSTGRES_PORT", "defaultPort": 5432, "protocol": "tcp" }
      ]
    }
  ]
}
```
