---
title: CLI
---

# AgentUp.CLI

`AgentUp.CLI` is a developer convenience layer over the Server.

Technology:

- .NET Console

## Purpose

The CLI forwards commands to the Server.

```bash
agent-up restart
agent-up stop
agent-up status
agent-up logs
```

## State Ownership

The CLI owns no state. It should not perform orchestration, port allocation, process management, browser control, or diagnostics collection itself.

## Relationship to MCP

MCP is the primary automation interface. The CLI is a human-friendly wrapper around server capabilities.
