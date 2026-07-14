---
title: CLI
---

# AgentUp.CLI

`AgentUp.CLI` is a developer convenience layer over the Server.

Technology:

- .NET Console

## Invoking with dotnet run

The CLI is run with `dotnet run`. Pass CLI arguments after `--`:

```bash
dotnet run --project AgentUp.CLI -- <command> [--server <url>]
```

The server URL defaults to `$AGENTUP_SERVER_URL` or `http://localhost:5000` when neither is set. Pass `--server` explicitly if the server is listening somewhere else, or set the environment variable:

```bash
export AGENTUP_SERVER_URL=http://localhost:5000
```

## Commands

### start

Reads `agent-up.json` from the current directory and pushes the workspace and application definitions to the server. Works like `npm install` — running it is what makes the workspace exist on the server. If the workspace has never been started, it does not exist. Running `start` again from the same directory updates the existing workspace in place.

```bash
dotnet run --project AgentUp.CLI -- start --server http://localhost:5000
```

### list

Lists all workspaces currently known to the server.

```bash
dotnet run --project AgentUp.CLI -- list --server http://localhost:5000
```

### status

Shows the state of the workspace in the current directory.

```bash
dotnet run --project AgentUp.CLI -- status --server http://localhost:5000
```

## State Ownership

The CLI owns no state. It should not perform orchestration, port allocation, process management, browser control, or diagnostics collection itself.

## Relationship to MCP

MCP is the primary automation interface. The CLI is a human-friendly wrapper around server capabilities.
