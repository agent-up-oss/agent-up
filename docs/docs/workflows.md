---
title: Workflows
---

# Workflows

Agent-Up is designed to support a complete AI validation workflow.

```text
Modify Code
↓
Restart Workspace
↓
Wait Until Healthy
↓
Inspect Page
↓
Navigate
↓
Interact
↓
Validate
↓
Take Screenshot
↓
Generate Playwright Test
↓
Commit
```

## No External Browser Harness

No browser automation framework should be required outside Agent-Up for routine validation.

AI agents should use the Server's MCP capabilities to inspect, navigate, interact, validate, screenshot, retrieve diagnostics, and export generated Playwright tests.

## Shared Human and Agent Context

Because humans and AI agents share the same browser session inside a workspace, validation can reuse authentication and application state instead of recreating it in a separate test browser.
