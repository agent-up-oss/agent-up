---
title: AUDebug
---

# AgentUp.AUDebug

<DocWhat>
`au-debug` hosts the repository Desktop, Mobile web export, and docs site so agents can screenshot and inspect those UIs without a packaged install. It is a maintainer visual-debug CLI, not an orchestration owner.
</DocWhat>

<DocFacts>
<DocFact label="Server">http://127.0.0.1:5001</DocFact>
<DocFact label="Docs">http://127.0.0.1:10100</DocFact>
</DocFacts>

`au-debug up` uses the repository Server on `http://127.0.0.1:5001` because Desktop and Mobile login need it. If that URL is already ready, `up` reuses it and `down` leaves it running. Otherwise `up` starts a repo Server and `down` stops Desktop, Mobile, docs, and that Server process.

## Run

From the repository root:

```bash
./au-debug up
```

or:

```bash
dotnet run --project AgentUp.AUDebug -- up
```

`up` streams child process logs to the terminal (prefixed `[server]`, `[desktop]`, `[mobile]`, `[docs]`) and also writes them under `.git/agent-up/au-debug/logs/`. It waits at most 30 seconds for readiness, then keeps hosting until Ctrl+C or `au-debug down` from another terminal.

Cold Desktop compiles can exceed 30 seconds. Pass `--timeout 120` for those runs. `--detach` returns after ready without following logs.

```bash
./au-debug up --timeout 120
./au-debug status
./au-debug desktop screenshot
./au-debug mobile screenshot
./au-debug docs screenshot
./au-debug docs screenshot / --full-page
./au-debug docs screenshot /docs/workspaces --full-page
./au-debug docs screenshot /developer-guide/workspaces --heading "Orchestration MCP"
./au-debug screenshots persist
./au-debug screenshots validate
./au-debug screenshots desktop git
./au-debug screenshots mobile apps
./au-debug screens
./au-debug screens desktop git
./au-debug desktop login
./au-debug mobile login
./au-debug desktop start-workspace Agent-Up
./au-debug desktop open-agent
./au-debug mobile open-agent Agent-Up
./au-debug down
```

`status` probes the hosted Server, Mobile, and docs URLs and checks that the Desktop window is present. Do not curl those ports or call `xdotool` from the shell; those checks belong inside `au-debug`.

`docs screenshot` captures the hosted Docusaurus site after `up` is ready. With no path it captures `/docs/`. Pass `/` for the marketing homepage, or a site path under `/docs/`, `/developer-guide/`, or `/design-system` to inspect one page. `--heading` scrolls that heading into the 1440x900 viewport before capture. Viewport captures always emulate that size so a 100% zoom check is the same on every host. `--full-page` captures the whole document. Both flags can be used together.

## Product screenshots

Docs and README images of Desktop and Mobile are rendered from the design-system catalog, not reconstructed marketing HTML. `au-debug screenshots persist` writes every major view into `media/`, and copies the Desktop applications hero to `media/screenshot.png`. Updating those images is that one command after `au-debug build design-system`.

`au-debug screenshots validate` is the identity gate: each scene may only use catalog `au-*` classes. Leaf components must appear as their catalog HTML; layout shells such as `screen` must appear by those catalog classes. It then re-renders each scene through Chromium and pixel-matches the files in `media/`. `--live` also dumps hosted Mobile pages after `au-debug up`.

```bash
./au-debug screenshots persist
./au-debug screenshots validate
./au-debug screenshots validate --live
./au-debug screenshots desktop
./au-debug screenshots mobile review
```

`persist` and `validate` default to a 180 second watchdog.

## Product screens

`au-debug screenshots` renders the design-system catalog. `au-debug screens` is the other
half: it drives the **real** Desktop window and the **real** Mobile web client through every
screen the design system documents under Assembled screens, and photographs each one. No
Server is involved — both clients connect to their built-in **Demo** entry, whose backend
runs in the client process.

Every screen is captured *used*, not freshly opened. The routes interact first: they sign in
by choosing Demo, send two prompts to the workspace agent, add two items to the Demo
storefront's cart, tick files and write a commit message, jump to a line in a diff, commit,
and disable a capability module. The routes are ordered and cumulative, because the Demo
backend answers a prompt by adding a file to the working tree: Git is captured after the
agent has run, and History after the commit those files went into.

```bash
./au-debug screens                 # Desktop and Mobile
./au-debug screens desktop         # one surface
./au-debug screens mobile review   # one screen
```

Output goes to `artifacts/product-screens/<surface>/<screen>.png`, with
`artifacts/product-screens/screens.json` accounting for every documented screen — including
the ones that were not captured, and why.

Four Desktop screens are in that second group. The Demo connection reports no Database,
Diagnostics, or Metrics entitlement, so Desktop does not build those tabs. Validation is a
collapsed sidebar on the selected application, not a dedicated screen. Capture the tab
surfaces against a real Server workspace instead.

`screens` starts what it needs and stops only that: the Desktop window, and the Mobile web
host on `http://127.0.0.1:10102`. A surface already running — because `au-debug up` is up —
is reused and left alone. Desktop is driven with `xdotool` and captured with ImageMagick's
`import`; Mobile is driven over the Chrome DevTools Protocol at 390x844.

Desktop click points live in `DesktopScreenGeometry` and were read off a real 1440x900 Demo
session, so a Desktop layout change moves those numbers and nothing else. Mobile addresses
controls by the accessible label the client gives them, so it needs no coordinates.

CI runs this in the **Product screens** job and uploads `artifacts/product-screens` as a
build artifact. It gates nothing; it is evidence to look at.

One caveat for repeat local runs: the Demo storefront keeps its cart and orders in the
Desktop WebView's own storage, so a second run on the same machine starts from the first
run's cart. A CI runner is new every time, which is where the output is meant to be read.

## Tests and builds

Visual-iteration checks run through `au-debug test`, not a separate `dotnet test` or `npm test` invocation. Rebuilds that agents otherwise repeat by hand — design-system `dist/` generation, Mobile typecheck, and the Mobile web export — run through `au-debug build`. Scoped suites keep the inner loop short; `test` / `test all` is the closing pass for a visual change.

```bash
./au-debug test design-system
./au-debug test desktop
./au-debug test mobile
./au-debug test au-debug
./au-debug test architecture
./au-debug test
./au-debug build design-system
./au-debug build mobile
./au-debug build
```

| Suite | Runs |
|---|---|
| `design-system` | `npm run build` then `npm test` in `AgentUp.DesignSystem` |
| `desktop` | `AgentUp.Desktop.Tests` |
| `mobile` | `npm run typecheck`, `npm test`, then `npm run build:web` in `AgentUp.Mobile` |
| `au-debug` | `AgentUp.AUDebug.Tests` |
| `architecture` | `AgentUp.Architecture.Tests` |
| `all` | those five, in that order |

| Build | Runs |
|---|---|
| `design-system` | `npm run build` in `AgentUp.DesignSystem` |
| `mobile` | `npm run typecheck` then `npm run build:web` in `AgentUp.Mobile` |
| `all` | those two, in that order |

`--timeout` defaults to 180 seconds for a scoped suite or build and 600 seconds for `all`.

Screenshot commands print `screenshot: <path>`. Login uses `--password` or `AGENTUP_ADMIN_PASSWORD` from the repository `.env` file.

## Watchdogs

Every one-shot command and `up` readiness uses a 30 second watchdog by default (`--timeout`). `up` itself is allowed to keep running after ready so logs stay attached; `down` also has a watchdog so stop cannot hang.

## Ports

| Surface | URL |
|---|---|
| Server | `http://127.0.0.1:5001` |
| Mobile web | `http://127.0.0.1:10102` |
| Docs | `http://127.0.0.1:10100` (`/`, `/docs/`, `/developer-guide/`, `/design-system`) |
| Desktop | native window titled `Agent-Up` |

Mobile `./au-debug build mobile` (or `./au-debug test mobile`) must have produced a web export before `up` serves Mobile. Desktop is launched through `run-desktop.sh`.

## Ownership

`AgentUp.AUDebug` must not take compile-time dependencies on Desktop, Mobile, Server, or CLI projects. It launches those surfaces as processes and drives windows or Chromium against them.
