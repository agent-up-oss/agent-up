---
title: Verification
---

<DocEyebrow slice="Verification" status="available" />

# Verification

<DocWhat>
Verification maps changed paths to named checks, runs those checks, and records receipts. It owns the `verification` and `coverage` objects in `agent-up.json` and the receipt ledger.

It never reads the agent commit queue. Record receipts before enqueue. This repository uses `enforcement: block`, so `guard_verification` and `agent-up verify guard` fail when receipts are missing or stale. MCP is loopback-only by default, like the other Agent-Up servers.
</DocWhat>

<DocMeta
  owner="AgentUp.Verification"
  tests="AgentUp.Verification.Tests"
  mcp="/mcp/verification"
/>

<DocSpine>
<DocBeat>Plan which checks apply</DocBeat>
<DocBeat>Run them and record receipts</DocBeat>
<DocBeat>Guard before enqueue</DocBeat>
</DocSpine>

<DocContract label="Tool">run_verification</DocContract>

## Tools

Every tool on `/mcp/verification` names its target with exactly one of `workspaceId` or `worktreePath`, resolved the same way the commit queue tools resolve theirs. Passing both, or neither, is a validation error, and a workspace-bound session passes neither.

<DocSteps>
<DocStep title="plan_verification">See which checks the current changes require, and which rule selected each.</DocStep>
<DocStep title="run_verification">Run every required check and record a receipt per check.</DocStep>
<DocStep title="run_verification_check">Re-run one check by id after a targeted fix.</DocStep>
<DocStep title="guard_verification">Report whether every required check has a passing receipt matching the current file contents.</DocStep>
<DocStep title="export_verification">Read a report of what was proven about this revision. Evidence to show, never an input to the guard.</DocStep>
</DocSteps>

<DocFacts label="Also">
<DocFact label="CLI">agent-up verify plan · run · guard · export</DocFact>
<DocFact label="Coverage">Runs as patch-coverage inside run_verification</DocFact>
<DocFact label="Receipts">{'.git/agent-up/verification/receipts.json'}</DocFact>
<DocFact label="Loop inference">slow ciOnly Category=LoopInference, excluded from the watchdog</DocFact>
<DocFact label="Loop soak">slow ciOnly Category=LoopSoak, excluded from the watchdog</DocFact>
</DocFacts>

## Export

Receipts live outside the working tree so one cannot travel in a change proposal and satisfy another machine's guard against bytes it never tested. Export closes that without weakening the ledger: `export_verification`, `GET /api/workspaces/{workspaceId}/verification/export`, and `agent-up verify export` emit the same report.

<DocFacts label="Export rules">
<DocFact label="Contains">Check ids, commands, exit codes, revision, digest</DocFact>
<DocFact label="Never contains">The per-file hash map</DocFact>
<DocFact label="Skipped checks">Reported with a reason, never counted as proven</DocFact>
<DocFact label="Guard">Must not be satisfiable by an exported document</DocFact>
</DocFacts>

The export is evidence to read, not an input to a gate. Keep the two types distinct so the confusion cannot be expressed in code.

The field contract is the [agent-up.json reference](/docs/configuration/reference#verification-object). Agent operating rules stay in `AGENTS.md`. Named QA journeys for Desktop and Mobile chrome live on the [QA coverage map](/developer-guide/verification/qa-coverage).

## Portable binary

`au-check` is the same resolver as `agent-up verify`, packaged so another repository can run the gate without building Agent-Up. `AgentUp.AUCheck` is a composition-only `Program.cs`. `CheckPlanProvider` is the only type that decides which checks are required, and both hosts print the same plan lines.

<DocContract label="Command">au-check plan</DocContract>

Download one release asset and run it from the repository you want to check. `--repo` defaults to the working directory. `--config` defaults to `agent-up.json` there. Receipts are written to that checkout's Git directory, the same ledger `agent-up verify` uses.

<DocFacts label="Assets">
<DocFact label="linux-x64">au-check-linux-x64</DocFact>
<DocFact label="linux-arm64">au-check-linux-arm64</DocFact>
<DocFact label="osx-arm64">au-check-osx-arm64</DocFact>
<DocFact label="osx-x64">au-check-osx-x64</DocFact>
<DocFact label="win-x64">au-check-win-x64.exe</DocFact>
</DocFacts>

<DocSteps>
<DocStep title="plan">Show the checks the current changes require, including any this machine cannot run.</DocStep>
<DocStep title="run">Run the runnable checks and record receipts. `run <check-id>` runs one.</DocStep>
<DocStep title="guard">Report whether every required check is proven. `--run` executes what is missing first.</DocStep>
<DocStep title="coverage">Measure changed lines. `slices` reports total coverage per feature slice.</DocStep>
</DocSteps>

<DocFacts label="Exit codes">
<DocFact label="0">Every required check ran and passed</DocFact>
<DocFact label="1">A required check ran and failed. A failure wins over a missing toolchain</DocFact>
<DocFact label="2">A required check could not run because a toolchain is missing</DocFact>
<DocFact label="3">Configuration is invalid, or a changed path matches no rule</DocFact>
</DocFacts>

`requires` on a check names `dotnet`, `node`, `npm`, `docker`, `git`, or `nix`, optionally with a minimum version. A missing tool skips that check. The skip is not a failed check and is not counted as satisfied, so `guard` still blocks. `ciOnly` and wrong-platform skips stay non-blocking. `agent-up verify` keeps its own exit codes.

`run --report <dir>` also writes `report.json`, `report.html`, and `report.png`. The HTML and the PNG lead with checks that could not run, then failures. Omitting `--report` leaves the run as it is today. The report does not change the exit code or the receipts.

<DocNext href="/developer-guide/commits" title="Commits">
Enqueue only after receipts pass.
</DocNext>
