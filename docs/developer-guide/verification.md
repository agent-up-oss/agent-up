---
title: Verification
---

# Verification

`AgentUp.Verification` owns the `verification` and `coverage` objects in `agent-up.json`, path-rule check selection, and the receipt ledger. It never reads the commit queue.

The MCP server is Streamable HTTP at `/mcp/verification`, with legacy SSE at `/mcp/verification/sse` plus `/mcp/verification/message`. Tools:

- `plan_verification`
- `run_verification`
- `run_verification_check`
- `guard_verification`

`agent-up verify coverage` has no MCP tool; it runs as the `patch-coverage` check inside `run_verification`. CLI equivalents are `agent-up verify plan`, `agent-up verify run [<check-id>]`, and `agent-up verify guard [--run] [--format hook]`.

Receipts live in `.git/agent-up/verification/receipts.json` and must be recorded before enqueue. The field contract is in the [agent-up.json reference](../user-docs/agent-up-json-reference.md#verification-object). Agent operating rules stay in `AGENTS.md`.
