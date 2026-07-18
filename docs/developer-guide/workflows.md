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

GitHub Actions runs the Agent-Up CI workflow on push. The version job uses semantic-release in dry-run mode on `main`, so it needs `contents: write` permission for semantic-release's repository push permission check even though it does not publish the release itself. The Ubuntu build job builds `agent-up.sln`, runs every `*Tests.csproj` project in deterministic path order, publishes TRX test results, collects Cobertura coverage through `coverlet.runsettings`, and publishes reusable .NET payloads for native package jobs. The native-display `AgentUp.Tests` project may retry once on Ubuntu, but failed attempts must preserve the failing test process exit code so aborted WebView runs cannot be reported as successful. Native release runners download those payloads, run platform packaging and smoke validation, and avoid restoring, building, or broadly testing product .NET projects.
