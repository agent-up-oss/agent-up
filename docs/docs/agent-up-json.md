---
title: agent-up.json
---

# agent-up.json

`agent-up.json` describes a repository's development applications and optional Docker startup commands.

## Fields

`name` identifies the workspace project.

`applications` lists launchable applications.

`docker` lists infrastructure commands the Server should run for the workspace.

## Application Fields

`name` is the display name for the application.

`command` is the shell command used to start the application.

`portVariable` is the environment variable that receives the Server-allocated port.

`path` is the browser path to open for that application.

## Port Variables

Agent-Up allocates ports per workspace and writes those values into environment variables.

```text
WEB_PORT=5100
API_PORT=5101
AUTH_PORT=5102
```

This keeps application source code free of Agent-Up dependencies.
