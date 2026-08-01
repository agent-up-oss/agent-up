---
title: Event Recording
---

# Event Recording

Every browser interaction becomes an event.

Examples:

- Navigation.
- Click.
- Keyboard.
- Text entry.
- DOM mutation.
- Console message.
- Network request.
- Screenshot.
- Dialog.
- Notification.

## Canonical Interaction History

The event stream is the canonical representation of user and agent interactions.

Playwright tests, diagnostics, workflow summaries, and future automation features should be derived from this event stream rather than from ad hoc command logs.

## Audit History

Agent-Up records durable audit events for browser MCP actions, screenshots, workspace and application state changes, source revision context, and action-relevant health/probe state. Audit records include workspace id, repository path, normalized worktree path, stable working-directory id, live branch, live commit SHA, dirty state when available, action outcome, and safe result details.

Screenshots are stored as Server-managed audit artifacts. Browser screenshot calls return a bounded MCP image content block for immediate agent inspection and an opaque artifact id that can be loaded later through Audit MCP without exposing temporary filesystem paths. Inline screenshots are captured at low browser-inspection resolution and rejected if the encoded image would exceed the MCP context budget.

## Intent Over Commands

Events capture what happened. Higher-level systems can infer why it happened and convert raw interactions into business workflows.
