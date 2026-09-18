---
title: Verification
---

<DocEyebrow slice="Verification" status="available" />

# Verification

<DocFocus>
Record receipts before enqueue. Verification never reads the commit queue.
</DocFocus>

**Owner:** `AgentUp.Verification`. Tests live in `AgentUp.Verification.Tests`. MCP: `/mcp/verification`. Field contract: [agent-up.json reference](/docs/configuration/reference#verification-object).

## What it is

`AgentUp.Verification` owns the `verification` and `coverage` objects in `agent-up.json`, path-rule check selection, and the receipt ledger.

<DocSpine>
<DocBeat selected>`plan_verification`</DocBeat>
<DocBeat>`run_verification`</DocBeat>
<DocBeat>`guard_verification` before enqueue</DocBeat>
</DocSpine>

<DocContract>/mcp/verification</DocContract>

The MCP server is Streamable HTTP at `/mcp/verification`, with legacy SSE at `/mcp/verification/sse` plus `/mcp/verification/message`. Loopback-only MCP access applies here the same as the other servers.

Tools:

- `plan_verification`
- `run_verification`
- `run_verification_check`
- `guard_verification`

`agent-up verify coverage` has no MCP tool; it runs as the `patch-coverage` check inside `run_verification`. CLI equivalents are `agent-up verify plan`, `agent-up verify run [<check-id>]`, and `agent-up verify guard [--run] [--format hook]`.

Receipts live in `.git/agent-up/verification/receipts.json` and must be recorded before enqueue. Agent operating rules stay in `AGENTS.md`.
