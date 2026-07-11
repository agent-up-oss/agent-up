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

## Intent Over Commands

Events capture what happened. Higher-level systems can infer why it happened and convert raw interactions into business workflows.
