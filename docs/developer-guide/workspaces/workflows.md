---
title: Workflows
---

# Workflows

<DocWhat>
Workflows is the intended AI validation loop: change code, restart the workspace, inspect, interact, and commit. It is the target path, not a hidden orchestration owner.
</DocWhat>

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

Because humans and AI agents share the same workspace, validation can reuse running applications and allocated ports. Desktop WebViews, Mobile WebViews, and the Server headless profile do not share cookies, storage, or navigation state. Replay of a validation flow uses the Desktop application WebView so a person can watch it.

## Continuous Integration

GitHub Actions runs the Agent-Up CI workflow on pull requests and on push to `main`. The version job uses semantic-release in dry-run mode on `main`, so it needs `contents: write` permission for semantic-release's repository push permission check even though it does not publish the release itself. A pull request is tested as GitHub's merge with its base.

CI is one workflow with one matrix job per domain; [CI](../repo/ci.md) is the definition. Build · release payloads is the only .NET leg whose artifacts packaging consumes. Source · .NET suites and coverage runs every `*Tests.csproj` except `AgentUp.Tests` once, in deterministic path order, with coverage, and publishes TRX results even when a suite fails. Desktop · Native Linux installs only the runtime WebKitGTK package the native-display tests need, retries browser-test dependency downloads and repairs interrupted apt package state between attempts, then runs `AgentUp.Tests` (native-display, then HeadlessE2E), excluding `RuntimeCapabilityE2E`. The native-display project may retry once on Ubuntu, but a failed attempt must preserve the failing test process exit code so an aborted WebView run cannot be reported as successful. Backend · Runtime capabilities starts the repository-root `agent-up.json` from Desktop with the `dotnet` and `docker` capabilities and replays the recorded Example Web flow, at detailed console verbosity and with `--blame-crash`. Backend · Forgejo host starts a digest-pinned Forgejo container and runs `Category=ForgejoE2E` against the first-party module and the Server Forge slice.

After the suites, the same leg judges patch and slice coverage from the Cobertura reports they wrote, in its own step, so a coverage miss and a failing test are two different red steps. It uploads each test project's reports to Codecov under its own flag, which preserves which test module covered each production project. `coverlet.runsettings` sets a 30-second default NUnit test timeout and a 1-minute testhost hang abort. Desktop · Native Linux uploads `AgentUp.Tests` coverage under the `agentup-tests` flag. The Desktop · Native macOS and Windows legs publish the framework-dependent `AgentUp.Tests` runner for their own runtime and run it against the .NET runtime they install, so Desktop waits for no other job.

When the `main` release job publishes a new semantic-release version, it uses that same version for `@agent-up/audit`, tests the package, and publishes it publicly to npm on the `latest` dist-tag. Other branches are pull requests and do not publish. Add an npm automation or granular access token as the `NPM_TOKEN` repository secret; when the secret is absent, npm publication is skipped without blocking the other release outputs.

The standalone LocalInstaller repository owns the product-neutral installer library CI workflow. It plans versions with semantic-release using `localinstaller-v${version}` tags, builds and tests `localinstaller.sln` on Ubuntu, macOS, and Windows, packs only the four `LocalInstaller.Core`, `LocalInstaller.App`, `LocalInstaller.Packaging`, and `LocalInstaller.Smoke` NuGet packages with the planned `LocalInstallerVersion`, publishes reusable self-contained sample product payloads, and runs native package smoke against `LocalInstaller.Sample.Packager` and `LocalInstaller.Sample.Smoke` on Ubuntu, macOS, and Windows runners. Sample product projects must remain non-packable. On `main`, when semantic-release finds a new LocalInstaller version, the workflow creates the `localinstaller-v*` GitHub release with separately labeled NuGet packages and sample native installer artifacts; NuGet publishing runs only when `NUGET_API_KEY` is configured.

Local verification for changes that affect packaged Desktop, InstallerApp, installed app layout, browser/WebView hosting, or CI payload staging must include the same native-display `AgentUp.Tests` command shape used by CI: Release configuration, `coverlet.runsettings`, TRX logging, coverage collection, and the Ubuntu WebKit/Xvfb environment when running on Linux.

The `linux-smoke` platform-tier verification check is locally runnable through `scripts/verify-linux-smoke.sh`. It self-publishes the five Linux product payloads and the packaging and smoke tools, packages them through the same release entry point as CI, and runs package validation plus packaged Server and CLI probes. A validated warm local run took 275 seconds, so this intentionally remains a minutes-long platform check rather than a fast check. macOS and Windows smoke remain `ciOnly` because their native jobs consume payloads cross-published by Ubuntu; local verification must report those checks as skipped with that reason rather than treating them as satisfied.
