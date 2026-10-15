---
title: Verification
---

<DocEyebrow slice="Verification" status="available" />

# Verification

<DocWhat>
Verification maps changed paths to named checks, runs those checks, and records receipts. It owns the `verification` and `coverage` objects in `agent-up.json` and the receipt ledger.

It never reads the agent commit queue. Record receipts before enqueue. MCP is loopback-only, like the other Agent-Up servers.
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

The field contract is the [agent-up.json reference](/docs/configuration/reference#verification-object). Agent operating rules stay in `AGENTS.md`.

<DocNext href="/developer-guide/commits" title="Commits">
Enqueue only after receipts pass.
</DocNext>
