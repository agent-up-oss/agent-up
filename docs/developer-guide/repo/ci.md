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
<DocBeat>Release and Mobile release wait for Behavior matrix and Plan, and never run together</DocBeat>
</DocSpine>

<DocContract label="Required check">Behavior matrix</DocContract>

Everything runs from `.github/workflows/ci.yml` on pull requests, pushes, and dispatches, including the Android and iOS store builds and their release; see [Mobile store release](./mobile-store-release.md). Only `mobile-ios-certs.yaml`, the one-time Match bootstrap, is a separate workflow. Secrets and variables are on [CI Configuration](./ci-configuration.md).

## Domains

Each domain is one job id (`build`, `source`, `backend`, `desktop`, `mobile`, `docs`, `installer`) with a matrix of legs. Legs are named `<Domain> · <what it proves>`, so the run graph reads as the matrix: one group per domain, one row per leg.

<DocFacts>
<DocFact label="Plan">The release version, and which Mobile release legs a dispatch ships. A single job.</DocFact>
<DocFact label="Build">The artifacts the desktop release ships, produced once: payloads, Server container, Helm chart, au-check, JetBrains plugin.</DocFact>
<DocFact label="Source">The .NET suites with patch and slice coverage, the TypeScript suites, test-tier budgets, performance gates. Gates release only.</DocFact>
<DocFact label="Backend">The Server's client contract, held against both Demos, and the Server against each real external dependency, one leg per dependency.</DocFact>
<DocFact label="Desktop">The Desktop client against a real Server, on its Demo, and on each native display.</DocFact>
<DocFact label="Mobile">The Mobile client and its chat module against a real Server, its Demo, the identity front door and each native platform, and its signed store binaries.</DocFact>
<DocFact label="Docs">The docs site and its API reference, and the design-system catalog the site publishes.</DocFact>
<DocFact label="Installer">Each native package installs, runs and uninstalls where it ships.</DocFact>
</DocFacts>

The `needs` edges follow artifacts, and nothing else:

- Build waits for Plan, which names the version it stamps.
- Installer waits for Build and Plan: it packages the payloads Build published.
- Source, Backend, Desktop, Mobile and Docs wait for nothing. Desktop's macOS and Windows legs publish their own native-display runner, and the Mobile store legs plan their own version.

Source gates release only. No other domain waits for a source suite or for coverage, so a failing test never hides what a behavior leg would have shown. `AgentUp.Architecture.Tests` `CiBehaviorMatrix` enforces that.

Long legs keep their steps in a composite action under `.github/actions/`, so a matrix entry stays one readable block: `build-payloads`, `source-coverage`, `sign-in-web`, `sign-in-android`, `sign-in-ios`, `mobile-store-android` and `mobile-store-ios`.

## Releases

Release and Mobile release sit on one tier and wait for the same two jobs, Behavior matrix and Plan.

- **Release** runs on a push when Plan says to publish. On `main` that is the stable semantic-release cut.
- On any other branch it is a GitHub prerelease `{last-stable-tag}-{runNumber}`, with `.{runAttempt}` only on a retry, to the same GitHub, Docker Hub, Helm, and npm destinations. It does not move `:latest`, npm `@latest`, or GitHub latest, and it skips JetBrains Marketplace.
- **Mobile release** runs on a `workflow_dispatch` whose `mobile-channel` is `beta` or `prod`. It has one leg per platform `mobile-platforms` names, and uploads the binaries the Mobile store legs built to Google Play and App Store Connect.

A dispatch never reaches Release, and a push never reaches Mobile release. A dispatch with `mobile-channel: none` runs the matrix and ships nothing. Push and dispatch runs never cancel, so a Release or Mobile release is not cut off; a second one on the same ref waits. Pull requests still cancel. A branch with an open pull request runs the matrix twice: the push can Release, the pull_request cannot.

## The behavior matrix

`.github/behavior-matrix.json` declares each domain as a stage, the question it answers, its matrix job, and its legs. Each leg says what it proves. A stage with axes — Desktop and Mobile — declares its grid, and every cell is either proven by a leg or listed as a gap with its reason:

- `gap`: nothing proves this cell yet.
- `elsewhere`: another pipeline proves it.
- `n/a`: the cell does not exist, such as sign-in against the Demo.

Every leg ends with `./.github/actions/record-verdict` under `if: always()`. The Behavior matrix job downloads those verdicts and runs `.github/scripts/behavior-matrix.mjs`. It writes the matrix to the run summary and fails when a leg failed or recorded nothing. A leg that never ran fails the run like a leg that ran and failed.

A leg that cannot run on some events declares `skippable` with the reason. Only such a leg may record a skipped verdict, and the summary names it as skipped with that reason, never as passed. The Mobile store legs are the only ones today: GitHub withholds their signing secrets from Dependabot and fork pull requests, and a mobile release of one platform has nothing to build for the other.

`CiBehaviorMatrix` keeps `ci.yml` and the matrix file in step:

- Every stage is one matrix job named after it.
- Every job other than Plan, Behavior matrix, Release and Mobile release is a stage.
- Every matrix `include` entry is a declared leg, and every leg is an `include` entry.
- Every leg records its verdict last, even when it failed, and records the job's status or a declared skip.
- Behavior matrix waits for every stage. Release runs only on pushes and Mobile release only on dispatches, and both wait for exactly the same jobs.

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

| | Real Server | Demo | Identity front door | Native platform |
|---|---|---|---|---|
| Classic shell | Journeys on a real Server | Showcase on the Demo | gap | Native Linux, macOS arm64, macOS x64, Windows |
| Command center | gap | gap | gap | gap |

- **Journeys** replay every Desktop QA flow of [QA coverage](../verification/qa-coverage.md) on an isolated real Server, one flow per `au-debug validate` run: `.github/scripts/run-journeys.sh desktop`.
- **Showcase** drives the real client on its Demo through every documented screen; a screen the route cannot reach fails the leg. Desktop cannot report its own text yet, so no screen is compared with the design system.
- **Native** legs run `AgentUp.Tests` on a real display: Linux from source, macOS and Windows from a framework-dependent runner each publishes for its own runtime with `.github/scripts/publish-native-e2e-runner.sh`. macOS arm64 runs on `macos-15` so it does not share the Xcode `macos-latest` pool that iOS store and sign-in use; GitHub cancelled that native leg when both targeted `macos-latest`.

## Mobile

| | Real Server | Demo | Identity front door | Native platform |
|---|---|---|---|---|
| Classic shell | Journeys on a real Server | Showcase on the Demo | Browser SSO front door | Android store bundle, iOS store archive |
| Command center | gap | gap | gap | gap |
| Chat module | Sign-in on the installable web | n/a | n/a | Sign-in on Android, Sign-in on iOS |

- **Journeys** replay every Mobile QA flow on the web export against an isolated real Server: `.github/scripts/run-journeys.sh mobile`.
- **Showcase** drives the real client on its Demo through every documented screen; a screen the route cannot reach fails the leg. On CI it drives Playwright's pinned Chromium, which the journeys use too. `.github/scripts/compare-product-screens.sh` then reports each screen's copy against the design system on the run summary. All 15 Mobile screens diverge today, so the comparison is a declared gap, not a gate, until the showcase is re-authored from the real screens.
- **Browser SSO front door** signs the web export in through `Examples/browser-sso` and checks that the client hides what its entitlement document forbids: `.github/scripts/run-browser-sso.sh`.
- **Sign-in** is the end-to-end agent sign-in suite against a real Server, test agents and identity provider. Each leg publishes the Server and test agents itself while it provisions its emulator or toolchain.
- **Store** legs build, sign and validate the full client as the AAB and IPA Mobile release ships. They prove the binaries build; no journey runs them on a device, which is a declared gap on each.

## Docs

| Leg | Proves |
|---|---|
| Site and API reference | The API reference matches the Server controllers, every page meets the page standard, and the site builds. |
| Design-system catalog | One palette, catalog-only paint, the scenes both command centers mount, and committed bindings that match their source. |

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
