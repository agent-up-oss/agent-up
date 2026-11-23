---
title: CI
---

# CI

<DocWhat>
This page is how Agent-Up CI is shaped and why. There is one job per domain, each job is a matrix of legs of one behavior matrix, and one check — Behavior matrix — says whether the commit proved what the matrix claims.
</DocWhat>

<DocSpine>
<DocBeat>Each domain is one matrix job, and each matrix entry is a leg of `.github/behavior-matrix.json` that records its verdict last</DocBeat>
<DocBeat>Behavior matrix renders every leg and gap on the run summary</DocBeat>
<DocBeat>Release waits for Behavior matrix and nothing else</DocBeat>
</DocSpine>

<DocContract label="Required check">Behavior matrix</DocContract>

Everything runs from `.github/workflows/ci.yml` on pull requests and pushes to `main`. The native store pipeline for Android and iOS stays in `.github/workflows/mobile-ci.yaml`; see [Mobile store release](./mobile-store-release.md). Secrets and variables are on [CI Configuration](./ci-configuration.md).

## Domains

Each domain is one job id (`build`, `source`, `backend`, `desktop`, `mobile`, `installer`) with a matrix of legs. Legs are named `<Domain> · <what it proves>`, so the run graph reads as the matrix: one group per domain, one row per leg.

<DocFacts>
<DocFact label="Plan">The release version. A single job.</DocFact>
<DocFact label="Build">Every artifact that ships, produced once: payloads, Server container, Helm chart, au-check, JetBrains plugin, docs site.</DocFact>
<DocFact label="Source">The code's own suites with patch and slice coverage, the TypeScript and design-system suites, test-tier budgets, performance gates. Gates release only.</DocFact>
<DocFact label="Backend">The Server's client contract, held against both Demos, and the Server against each real external dependency, one leg per dependency.</DocFact>
<DocFact label="Desktop">The Desktop client against a real Server, on its Demo, and on each native display.</DocFact>
<DocFact label="Mobile">The Mobile client and its chat module against a real Server, on its Demo, and on each native platform.</DocFact>
<DocFact label="Installer">Each native package installs, runs and uninstalls where it ships.</DocFact>
</DocFacts>

The `needs` edges follow artifacts:

- Build waits for Plan, which names the version it stamps.
- Desktop and Installer wait for Build. They run what it published: the native-display runner for macOS and Windows, and the release payloads. Desktop's Linux legs build from source and do not need Build, so waiting delays them by Build's duration. That is the price of one Desktop group instead of two.
- Source, Backend and Mobile wait for nothing.

Source gates release only. No Backend, Desktop, Mobile or Installer leg waits for a source suite or for coverage, so a failing test never hides what a behavior leg would have shown. `AgentUp.Architecture.Tests` `CiBehaviorMatrix` enforces that.

Long legs keep their steps in a composite action under `.github/actions/`, so a matrix entry stays one readable block: `build-payloads`, `source-coverage`, and `sign-in-web`, `sign-in-android` and `sign-in-ios`.

## The behavior matrix

`.github/behavior-matrix.json` declares each domain as a stage, the question it answers, its matrix job, and its legs. Each leg says what it proves. A stage with axes — Desktop and Mobile — declares its grid, and every cell is either proven by a leg or listed as a gap with its reason:

- `gap`: nothing proves this cell yet.
- `elsewhere`: another pipeline proves it, such as Mobile CI building the native client.
- `n/a`: the cell does not exist, such as sign-in against the Demo.

Every leg ends with `./.github/actions/record-verdict` under `if: always()`. The Behavior matrix job downloads those verdicts and runs `.github/scripts/behavior-matrix.mjs`. It writes the matrix to the run summary and fails when a leg failed or recorded nothing. A leg that never ran fails the run like a leg that ran and failed.

`CiBehaviorMatrix` keeps `ci.yml` and the matrix file in step:

- Every stage is one matrix job named after it.
- Every job other than Plan, Behavior matrix and Release is a stage.
- Every matrix `include` entry is a declared leg, and every leg is an `include` entry.
- Every leg records its verdict last, even when it failed.
- Behavior matrix waits for every stage, and Release waits for Behavior matrix.

### Adding a leg

1. Declare it under its domain in `.github/behavior-matrix.json` with what it proves.
2. Add the matrix `include` entry to that domain's job in `ci.yml`. Gate its steps on `matrix.leg`, or give it its own composite action.

A gap that gains a leg loses its gap entry in the same change. A new domain is a new stage, a new matrix job, and a new entry in Behavior matrix `needs`.

## Backend

### Contract

`AgentUp.FakeServer/parity.json` lists every operation in `docs/api-reference/openapi.json` once.

- A read records the real Server's answer: status, body kind, and top-level keys.
- A read the Demo shares answers the same, or declares `omits` and `extra` with `because`.
- Where the probe Server cannot answer without a browser, a saved flow or a picture, the Demo states its own `keys`.
- An operation only a real Server answers is `absent`, with the reason.
- A write says whether the Demo answers it.

| Leg | Holds to the manifest |
|---|---|
| Contract · Server | A published Server with a registered probe workspace, over HTTP: `.github/scripts/run-backend-parity.sh server` |
| Contract · Demo (Desktop) | Desktop's in-process Demo: `DemoParityTests` (`Category=DemoParity`) |
| Contract · Demo (Mobile) | Mobile's in-process Demo: `FakeBackendParity.test.ts` |

`AgentUp.FakeServer/parity.mjs` holds the rules the Server leg and the Mobile test share; `DemoParityTests` follows them in C#.

A Server read that changes shape fails the Server leg. After a deliberate change, re-record and decide, per changed read, whether the Demo answers it too:

```bash
./.github/scripts/run-backend-parity.sh server --record
```

### Dependencies

One leg per real dependency, so a red leg names what broke. Each leg runs one script; the agent legs pass one category to `run-agent-e2e.sh`.

| Leg | Dependency | Script |
|---|---|---|
| Forgejo host | Forgejo 11 container | `run-forgejo-e2e.sh` |
| Agent team on Forgejo | Forgejo, bubblewrap, Chromium | `run-agent-e2e.sh TeamForgejoE2E` |
| Agent sandbox | bubblewrap | `run-agent-e2e.sh AgentSandboxE2E` |
| Agent over MCP | bubblewrap, Chromium | `run-agent-e2e.sh AgentMcpE2E` |
| Loop on CPU inference | pinned GGUF on llama.cpp | `run-loop-inference.sh` |
| OpenCode on CPU inference | opencode-ai, test-model-server | `run-opencode-inference.sh` |
| Runtime capabilities | Nix, Docker, Postgres | `run-runtime-capability-e2e.sh` |
| Loop soak | none | `run-loop-soak.sh` |

## Desktop

| | Real Server | Demo | Native platform |
|---|---|---|---|
| Classic shell | Journeys on a real Server | Showcase on the Demo | Native Linux, macOS arm64, macOS x64, Windows |
| Command center | gap | gap | gap |

- **Journeys** replay every Desktop QA flow of [QA coverage](../verification/qa-coverage.md) on an isolated real Server, one flow per `au-debug validate` run: `.github/scripts/run-journeys.sh desktop`.
- **Showcase** drives the real client on its Demo through every documented screen; a screen the route cannot reach fails the leg. Desktop cannot report its own text yet, so no screen is compared with the design system.
- **Native** legs run `AgentUp.Tests` on a real display: Linux from source, macOS and Windows from the runner Build published for them.

## Mobile

| | Real Server | Demo | Native platform |
|---|---|---|---|
| Classic shell | Journeys on a real Server | Showcase on the Demo | elsewhere: Mobile CI |
| Command center | gap | gap | elsewhere: Mobile CI |
| Chat module | Sign-in on the installable web | n/a | Sign-in on Android, Sign-in on iOS |

- **Journeys** replay every Mobile QA flow on the web export against an isolated real Server: `.github/scripts/run-journeys.sh mobile`.
- **Showcase** drives the real client on its Demo through every documented screen; a screen the route cannot reach fails the leg. `.github/scripts/compare-product-screens.sh` then reports each screen's copy against the design system on the run summary. All 15 Mobile screens diverge today, so the comparison is a declared gap, not a gate, until the showcase is re-authored from the real screens.
- **Sign-in** is the end-to-end agent sign-in suite against a real Server, test agents and identity provider. Each leg publishes the Server and test agents itself while it provisions its emulator or toolchain.

## Installer

| Leg | Proves natively | Not proven yet |
|---|---|---|
| Ubuntu .deb | dpkg install, systemd service, CLI, uninstall | |
| NixOS flake | flake build, built Server and CLI | the NixOS module is never booted |
| macOS arm64 / x64 .pkg | package contract, packaged Server and CLI, installer startup and elevation | the installed launchd service |
| Windows installer | install, Windows service, CLI, uninstall | packaged Server before install |

The guided installer flow runs with `AGENTUP_INSTALLER_FAKE=1` on every leg. Installers wait for Build and Plan only.

<DocNext href="/developer-guide/repo/ci-configuration" title="CI Configuration">
The secrets and variables these jobs read.
</DocNext>
