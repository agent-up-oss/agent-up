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

## Continuous Integration

GitHub Actions runs the Agent-Up CI workflow on push. The Ubuntu build job builds `agent-up.sln`, runs every `*Tests.csproj` project, publishes TRX test results, collects Cobertura coverage through `coverlet.runsettings`, and publishes reusable .NET payloads for native package jobs. Native release runners download those payloads, run platform packaging and smoke validation, and avoid restoring, building, or broadly testing product .NET projects. Sonar still runs on Ubuntu using the `agent-up` project key.
