---
title: agent-up.json
---

# agent-up.json

`agent-up.json` describes a repository's development applications and optional Docker startup commands.

## Fields

`name` identifies the workspace project.

`applications` lists launchable applications.

`services` lists Docker services the Server should run for the workspace.

## Application Fields

`name` is the display name for the application.

`command` is the shell command used to start the application. Agent-Up runs it through the host platform shell: `cmd.exe /C` on Windows and Bash on Unix-like systems.

`path` is the browser path to open for that application.

`ports` lists port declarations for the application.

## Service Fields

`name` is the display name for the Docker service.

`image` is the Docker image for the service.

`ports` lists port declarations for the service.

`environment` optionally defines service environment variables.

`volumes` optionally defines Docker volume mappings.

## Port Fields

`variable` is the environment variable that receives the Server-allocated port.

`defaultPort` is the preferred/default port used to derive allocation intent.

`protocol` is the protocol label, usually `http` or `tcp`.

## Port Variables

Agent-Up allocates ports per workspace and writes those values into environment variables. Each launched local application receives the full workspace port map, so a frontend can discover the API port and an API can discover infrastructure ports without hardcoding localhost values.

```text
WEB_PORT=5100
API_PORT=5101
AUTH_PORT=5102
```

This keeps application source code free of Agent-Up dependencies.
