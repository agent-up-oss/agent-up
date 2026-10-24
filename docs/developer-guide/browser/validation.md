---
title: Playwright Generation
---

# Playwright Generation

<DocWhat>
This page is the Playwright export from a saved validation flow. Desktop Validation sidebar replay and `export_validation_flow` emit that test today.
</DocWhat>

## Direction

Generated tests should prefer semantic locators, avoid brittle selectors, generate assertions from visible outcomes, produce readable code, and follow Playwright best practices. Inferring user intent from raw history is not the current exporter.

## Behavioral validation flows

A validation flow belongs to one workspace application and starts at an application-relative initial path. It records the meaningful route through the GUI plus an observable expectation at the initial place and after each step. Agents must describe user intent and visible behavior—not DOM structure, framework components, backend calls, or other implementation detail. A flow names a user's intent, not a click path. Each step has one action and at least one expectation of the visible consequence; a step whose only assertion is that the clicked control still exists is rejected. Visible or Enabled with value `false` is a hidden or disabled outcome, not that warning. First-suite Desktop and Mobile chrome journeys are declared on the [QA coverage map](/developer-guide/verification/qa-coverage).

Flows may also declare `surface: Desktop` to target Agent-Up Desktop chrome instead of a workspace application. Those flows persist with application `Desktop`, treat `initialPath` as a named route (`login`, `workspaces/list`, `workspace/overview`, `workspace/applications`, `workspace/git`, `workspace/agents`), and address controls with `testId` mapped to Avalonia `AutomationId`. `Selector`, `X`, and `Y` are rejected. Application-surface flows stay the default when `surface` is omitted, so existing `.agent-up/validation-flows.json` files keep working. Playwright export stays Application-surface only.

Desktop-surface replay runs on the real Desktop window, not in the Validation sidebar WebView. `au-debug validate --surface desktop` launches Desktop with `AGENTUP_VALIDATE_*` and writes `<out>/<flow-id>/report.json`, per-step PNGs, and `server.log`. Defaults `--out` to `artifacts/desktop-validation`. Cosmetic mouse staging is off for that command. Interactive Desktop controls must carry `AutomationProperties.AutomationId`; `AgentUp.Architecture.Tests` fails a view that omits one.

`surface: Mobile` is the same schema for Agent-Up Mobile chrome. Those flows persist with application `Mobile`, treat `initialPath` as an expo-router path (`/connect`, `/workspace/{workspaceId}/git`), keep `Url` expectations, and address controls with role, label, name, text, or `testId`. `Selector`, `X`, and `Y` are rejected. The web/PWA client is the runner today: `au-debug validate --surface mobile` drives the real Mobile export against a real Server and writes `report.json` plus per-step screenshots. Native Detox coverage is later; do not deep-link past the connect form.

The Server persists flow definitions in the workspace project at `.agent-up/validation-flows.json`, so validation checks are versioned with the repository. Supplying an existing flow ID replaces it as a new version, which supports editing and re-recording without creating duplicates. The Desktop Validation sidebar appears only while an application is selected, starts collapsed, lists checks for that application, expands each flow into its steps and assertions while replay runs, and shows pass or fail icons for individual checks, stages, and the overall flow. Replay plays in the embedded WebView with staged mouse movement, half-second attention pings, navigation, and page-load waits so the user can follow the journey.

CI proves the first-party `docker` and `dotnet` capabilities by starting the repository-root `agent-up.json` from Desktop and replaying the recorded Example Web flow. That check lives in `AgentUp.Tests` under `Category=RuntimeCapabilityE2E` and runs in the Runtime capability E2E job rather than the 180-second GUI E2E watchdog. Desktop `POST /start` waits for those launches to finish; the fixture heartbeats application state during that wait and does not occupy the Avalonia UI thread for it.

Playwright export prefers role, accessible name, label, test ID, and visible text locators in that order; CSS is only an export fallback; click and fill steps retain a stable selector fallback so the same flow can also replay in the Desktop WebView. Agents should infer routes and controls from application source or router files when that is quicker than inspecting every page, perform the journey once, then call `save_validation_flow` with user-meaningful descriptions and visible expectations after each step. Navigation is restricted to single-slash application-relative paths and exports resolve them against `AGENT_UP_BASE_URL`, preventing a recorded flow from escaping to another origin while allowing the same check to use dynamically allocated local ports and a CI-provided headless target. Assertions belong after the action whose user-visible outcome they describe, rather than relying on sleeps or incidental implementation state.
