---
title: Event Recording
---

# Event Recording

Shipped today: Browser MCP actions, screenshots, workspace and application state changes, captured application console lines, source revision context, and action-relevant health/probe state are written to the Server audit store. Agents query that history through Audit MCP.

## Direction

Every browser interaction and relevant runtime signal should become an event so Playwright tests, diagnostics, workflow summaries, and future automation can be derived from one history rather than ad hoc command logs. Intent inference from that stream is not built yet.

## Audit History

Agent-Up records durable audit events for browser MCP actions, screenshots, workspace and application state changes, captured application console lines, source revision context, and action-relevant health/probe state. Audit records include workspace id, repository path, normalized worktree path, stable working-directory id, live branch, live commit SHA, dirty state when available, action outcome, and safe result details.

Captured application console messages are redacted before they are written to durable audit details, and MCP console snapshots redact live console lines before returning them to clients.

Screenshots are stored as Server-managed audit artifacts. Browser screenshot calls return a bounded MCP image content block for immediate agent inspection and an opaque artifact id that can be loaded later through Audit MCP without exposing temporary filesystem paths. Inline screenshots are captured at low browser-inspection resolution and rejected if the encoded image would exceed the MCP context budget.

## Intent Over Commands

Events capture what happened. Higher-level systems can infer why it happened and convert raw interactions into business workflows.
