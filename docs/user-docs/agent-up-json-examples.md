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
