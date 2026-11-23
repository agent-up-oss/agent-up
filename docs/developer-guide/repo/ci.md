---
title: CI
---

# CI

<DocWhat>
This page is how Agent-Up CI is shaped and why. Every job answers one question, every job is a leg of one behavior matrix, and one check — Behavior matrix — says whether the commit proved what the matrix claims.
</DocWhat>

<DocSpine>
<DocBeat>Each job is a leg of `.github/behavior-matrix.json` and records its verdict last</DocBeat>
<DocBeat>Behavior matrix renders every leg and gap on the run summary</DocBeat>
<DocBeat>Release waits for Behavior matrix and nothing else</DocBeat>
</DocSpine>

<DocContract label="Required check">Behavior matrix</DocContract>

Everything runs from `.github/workflows/ci.yml` on pull requests and pushes to `main`. The native store pipeline for Android and iOS stays in `.github/workflows/mobile-ci.yaml`; see [Mobile store release](./mobile-store-release.md). Secrets and variables are on [CI Configuration](./ci-configuration.md).

## Stages

Jobs are named `<Stage> · <what it proves>`, so the job list reads as the matrix.

<DocFacts>
<DocFact label="Plan">The release version.</DocFact>
<DocFact label="Build">Every artifact that ships, produced once: payloads, Server container, Helm chart, au-check, JetBrains plugin, docs site.</DocFact>
<DocFact label="Source">The code's own suites, patch and slice coverage, test-tier budgets, performance gates. Gates release only.</DocFact>
<DocFact label="Backend">The real Server and both Demo backends answer one client contract.</DocFact>
<DocFact label="Integration">The Server against each real external dependency, one leg per dependency.</DocFact>
<DocFact label="Frontend">Each client, in each shell, against each backend and on each native display.</DocFact>
<DocFact label="Installer">Each native package installs, runs and uninstalls where it ships.</DocFact>
</DocFacts>

Source gates release only. No Backend, Integration, Frontend or Installer leg waits for a source suite or for coverage, so a failing test never hides what a behavior leg would have shown. `AgentUp.Architecture.Tests` `CiBehaviorMatrix` enforces that.

## The behavior matrix

`.github/behavior-matrix.json` declares each stage, the question it answers, and its legs. Each leg says what it proves. A stage with axes — Frontend — declares its grid, and every cell is either proven by a leg or listed as a gap with its reason:

- `gap`: nothing proves this cell yet.
- `elsewhere`: another pipeline proves it, such as Mobile CI building the native client.
- `n/a`: the cell does not exist, such as sign-in against the Demo.

Every leg ends with `./.github/actions/record-verdict` under `if: always()`. The Behavior matrix job downloads those verdicts and runs `.github/scripts/behavior-matrix.mjs`. It writes the matrix to the run summary and fails when a leg failed or recorded nothing. A leg that never ran fails the run like a leg that ran and failed.

`CiBehaviorMatrix` keeps `ci.yml` and the matrix file in step:

- Every job is a declared leg, and every leg is a job.
- Every matrix `include` entry is a declared leg.
- Every leg records its verdict last, even when it failed.
- Behavior matrix waits for every leg, and Release waits for Behavior matrix.

### Adding a leg

1. Declare it in `.github/behavior-matrix.json` with what it proves.
2. Add the job or matrix `include` entry in `ci.yml`, named `<Stage> · <title>`.
3. End the job with `record-verdict`, and add it to Behavior matrix `needs`.

A gap that gains a leg loses its gap entry in the same change.

## Backend

`AgentUp.FakeServer/parity.json` lists every operation in `docs/api-reference/openapi.json` once.

- A read records the real Server's answer: status, body kind, and top-level keys.
- A read the Demo shares answers the same, or declares `omits` and `extra` with `because`.
- Where the probe Server cannot answer without a browser, a saved flow or a picture, the Demo states its own `keys`.
- An operation only a real Server answers is `absent`, with the reason.
- A write says whether the Demo answers it.

| Leg | Holds to the manifest |
|---|---|
| Server | A published Server with a registered probe workspace, over HTTP: `.github/scripts/run-backend-parity.sh server` |
| Demo (Desktop) | Desktop's in-process Demo: `DemoParityTests` (`Category=DemoParity`) |
| Demo (Mobile) | Mobile's in-process Demo: `FakeBackendParity.test.ts` |

`AgentUp.FakeServer/parity.mjs` holds the rules the Server leg and the Mobile test share; `DemoParityTests` follows them in C#.

A Server read that changes shape fails the Server leg. After a deliberate change, re-record and decide, per changed read, whether the Demo answers it too:

```bash
./.github/scripts/run-backend-parity.sh server --record
```

## Integration

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

## Frontend

| | Real Server | Demo | Native platform |
|---|---|---|---|
| Desktop · classic | Journeys · Desktop | Showcase · Desktop | Native desktop · Linux, macOS arm64, macOS x64, Windows |
| Desktop · command center | gap | gap | gap |
| Mobile · classic | Journeys · Mobile | Showcase · Mobile | elsewhere: Mobile CI |
| Mobile · command center | gap | gap | elsewhere: Mobile CI |
| Mobile · chat module | Sign-in · installable web | n/a | Sign-in · Android, iOS |

- **Journeys** replay every QA flow of [QA coverage](../verification/qa-coverage.md) on an isolated real Server, one flow per `au-debug validate` run: `.github/scripts/run-journeys.sh desktop|mobile`.
- **Showcase** drives the real client on its Demo through every documented screen; a screen the route cannot reach fails the leg. `au-debug screens compare` then reports each screen's copy against the design system on the run summary. All 15 Mobile screens diverge today, so the comparison is a declared gap, not a gate, until the showcase is re-authored from the real screens. Desktop cannot report its own text yet. One client per job.
- **Sign-in** is the end-to-end agent sign-in suite against a real Server, test agents and identity provider.
- **Native desktop** is `AgentUp.Tests` on a real display.

## Installer

| Leg | Proves natively | Not proven yet |
|---|---|---|
| Ubuntu .deb | dpkg install, systemd service, CLI, uninstall | |
| NixOS flake | flake build, built Server and CLI | the NixOS module is never booted |
| macOS arm64 / x64 .pkg | package contract, packaged Server and CLI, installer startup and elevation | the installed launchd service |
| Windows installer | install, Windows service, CLI, uninstall | packaged Server before install |

The guided installer flow runs with `AGENTUP_INSTALLER_FAKE=1` on every leg. Installers wait for Build · release payloads only.

<DocNext href="/developer-guide/repo/ci-configuration" title="CI Configuration">
The secrets and variables these jobs read.
</DocNext>
