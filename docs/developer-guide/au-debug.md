---
title: AUDebug
---

# AgentUp.AUDebug

`AgentUp.AUDebug` is a maintainer visual-debug CLI named `au-debug`. It is similar in shape to `AgentUp.CLI`, but it does not wrap Server orchestration for users. It hosts the **repository** Desktop, Mobile web export, and docs site side by side so agents and maintainers can inspect those UIs without using a packaged Agent-Up install.

`au-debug up` also starts the repository Server on `http://127.0.0.1:5001` because Desktop and Mobile login need it. `au-debug down` stops Desktop, Mobile, docs, and that Server process.

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
./au-debug desktop login
./au-debug mobile login
./au-debug desktop start-workspace Agent-Up
./au-debug down
```

`status` probes the hosted Server, Mobile, and docs URLs and checks that the Desktop window is present. Do not curl those ports or call `xdotool` from the shell; those checks belong inside `au-debug`.

Screenshot commands print `screenshot: <path>`. Login uses `--password` or `AGENTUP_ADMIN_PASSWORD` from the repository `.env` file.

## Watchdogs

Every one-shot command and `up` readiness uses a 30 second watchdog by default (`--timeout`). `up` itself is allowed to keep running after ready so logs stay attached; `down` also has a watchdog so stop cannot hang.

## Ports

| Surface | URL |
|---|---|
| Server | `http://127.0.0.1:5001` |
| Mobile web | `http://127.0.0.1:10102` |
| Docs | `http://127.0.0.1:10100` (design-system at `/design-system`) |
| Desktop | native window titled `Agent-Up` |

Mobile `npm run serve:web` requires a prior `npm run build:web` in `AgentUp.Mobile/`. Desktop is launched through `run-desktop.sh`.

## Ownership

`AgentUp.AUDebug` must not take compile-time dependencies on Desktop, Mobile, Server, or CLI projects. It launches those surfaces as processes and drives windows or Chromium against them.
