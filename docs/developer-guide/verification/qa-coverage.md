---
title: QA coverage map
---

# QA coverage map

<DocWhat>
This page is the declared QA map for Agent-Up's own user-facing surfaces. Each cell is `covered`, `gap`, `blocked by #N`, or `n/a`. A surface with no flow is a recorded gap, not an accident.
</DocWhat>

A pull request that adds a user-facing Desktop or Mobile surface updates this table in the same change. `AgentUp.Architecture.Tests` fails an incomplete table, an empty cell, or a first-suite flow that is missing from the runners.

The first suite runs from `au-debug validate --surface desktop` and `au-debug validate --surface mobile`. Each command writes `<out>/<flow-id>/report.json`, numbered step screenshots, and `server.log`. Fixtures register the repository-root `agent-up.json` workspace on an isolated real Server. They do not assume a developer's dirty tree or an enabled forge module.

| Surface | Journey | Desktop | Mobile |
|---|---|---|---|
| First run / connect | Enter a server URL, connect, reach a workspace | covered | covered |
| First run / Demo | Connect to the built-in Demo server | gap | gap |
| Workspace list | Empty state, then a registered workspace appears | covered | covered |
| Workspace lifecycle | Start, applications reach Running, stop | covered | covered |
| Application tabs | Select an application, see its port and state | covered | covered |
| Console | Open an application console, see real output | covered | n/a |
| Diagnostics | See entries for a failed application | covered | n/a |
| Git review | Working-tree changes and diff | covered | covered |
| Git history | Commit history | gap | gap |
| Commits | Commit queue / proposal queue | gap | gap |
| Agents | Agent availability, remedies, start a session, transcript | covered | covered |
| Autonomous loop / checklist | Precondition checklist | covered | covered |
| Autonomous loop / activate | Activate after a satisfied checklist, typed terminal | gap | gap |
| Forge review / hosting | Hosting strip notInstalled with bootstrap remedy | covered | covered |
| Forge review / proposal | Open a proposal, checks, review threads | gap | gap |
| Capabilities | List modules, enable one | gap | gap |
| Validation panel | List flows for an application, replay one | covered | n/a |
| Browser isolation | Two workspaces, two profiles, no shared session | gap | n/a |
| Settings | Server switch, sign-out | gap | gap |
| Example Web app | The existing recorded Application-surface flow | covered | n/a |
| Example API | API-backed page renders seeded rows | gap | n/a |
| Installer | Install, launch, uninstall | blocked by #527 | n/a |

## n/a

Every `n/a` is a surface that does not exist on that client:

- **Console (Mobile).** Mobile has no application console tab. Output stays on Desktop.
- **Diagnostics (Mobile).** Mobile has no Diagnostics / audit tab.
- **Validation panel (Mobile).** The Validation sidebar is Desktop-only and appears once an application is selected.
- **Browser isolation (Mobile).** The PWA does not host per-workspace Desktop-style WebView profiles.
- **Example Web app (Mobile).** That journey is an Application-surface flow against a hosted HTTP app, not Mobile chrome.
- **Example API (Mobile).** API-backed pages are Application-surface flows, not Mobile chrome.
- **Installer (Mobile).** LocalInstaller is a desktop dashboard. It is not a Mobile surface. Desktop stays `blocked by #527`.

Workspace lifecycle is not `n/a` on Mobile: the Apps dashboard exposes Start workspace and Stop workspace.

## First-suite flows

| Journey | Desktop flow id | Mobile flow id |
|---|---|---|
| First run / connect | `connect` | `connect` |
| Workspace list | `connect` | `connect` |
| Workspace lifecycle | `workspace-lifecycle` | `workspace-lifecycle` |
| Application tabs | `console-diagnostics` | `workspace-lifecycle` |
| Console and diagnostics | `console-diagnostics` | n/a |
| Git review | `git-review` | `git-review` |
| Validation panel | `validation-panel` | n/a |
| Agents (#529 merged) | `agents` | `agents` |
| Autonomous loop / checklist (#526 merged) | `autonomous-loop` | `autonomous-loop` |
| Forge review / hosting (#525 merged) | `forge-hosting` | `git-review` |
| Example Web | `b6e1c8a04f5d4e2f9a7c3d1e8b0f2468` (Application) | n/a |

Agents, autonomous loop, and forge are no longer blocked. The isolated Server has no forge module and no host credential, so those flows cover the shipping empty states: agent availability and remedies, the unattended-loop checklist, and the Git hosting strip's `notInstalled` copy with `Run: agent-up capabilities bootstrap`. Starting `test-agent-scripted` covers a live session on a Development Server. The isolated Server puts that executable on PATH through a fixture shim. Opening a forge proposal, activating a loop through a satisfied checklist, and Demo remain gaps.

Workspace start in this suite asserts the workspace leaves Stopped (Stop workspace is visible). Stop is disabled until Running or Degraded, and every application reaching `Running` is the Runtime capability E2E job; a cold start of this repository exceeds the validation wait. Console, diagnostics, and the Validation sidebar select Docs without starting processes.

## Authoring

A flow names a user's intent, not a click path. Each step has one action and at least one expectation of the visible consequence. Server save rejects Selector/X/Y on Desktop and Mobile surfaces, and rejects a click or fill whose only Visible/Enabled expectation is that the same control still exists. Visible or Enabled with value `false` is a hidden or disabled outcome, not that warning. Flows are independently runnable and start at the connect surface; they do not deep-link past chrome.

<DocNext href="/developer-guide/browser/validation" title="Playwright generation">
How Application, Desktop, and Mobile flows are stored and replayed.
</DocNext>
