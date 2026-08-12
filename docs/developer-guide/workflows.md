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

The LocalInstaller CI workflow mirrors that release shape for the product-neutral installer libraries. It plans versions with semantic-release using `localinstaller-v${version}` tags, builds and tests `localinstaller.sln` on Ubuntu, macOS, and Windows, packs only the four `LocalInstaller.Core`, `LocalInstaller.App`, `LocalInstaller.Packaging`, and `LocalInstaller.Smoke` NuGet packages with the planned `LocalInstallerVersion`, publishes reusable self-contained sample product payloads, and runs native package smoke against `LocalInstaller.Sample.Packager` and `LocalInstaller.Sample.Smoke` on Ubuntu, macOS, and Windows runners. Sample product projects must remain non-packable. On `main`, when semantic-release finds a new LocalInstaller version, the workflow creates the `localinstaller-v*` GitHub release with separately labeled NuGet packages and sample native installer artifacts; NuGet publishing runs only when `NUGET_API_KEY` is configured.

Local verification for changes that affect packaged Desktop, InstallerApp, installed app layout, browser/WebView hosting, or CI payload staging must include the same native-display `AgentUp.Tests` command shape used by CI: Release configuration, `coverlet.runsettings`, TRX logging, coverage collection, and the Ubuntu WebKit/Xvfb environment when running on Linux.
