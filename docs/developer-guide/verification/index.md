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

<DocSteps>
<DocStep title="plan_verification">See which checks the current changes require, and which rule selected each.</DocStep>
<DocStep title="run_verification">Run every required check and record a receipt per check.</DocStep>
<DocStep title="run_verification_check">Re-run one check by id after a targeted fix.</DocStep>
<DocStep title="guard_verification">Report whether every required check has a passing receipt matching the current file contents.</DocStep>
</DocSteps>

<DocFacts label="Also">
<DocFact label="CLI">agent-up verify plan · run · guard</DocFact>
<DocFact label="Coverage">Runs as patch-coverage inside run_verification</DocFact>
<DocFact label="Receipts">{'.git/agent-up/verification/receipts.json'}</DocFact>
</DocFacts>

## Planned: receipt export

Receipts live outside the working tree so one cannot travel in a change proposal and satisfy another machine's guard against bytes it never tested. The side effect is that the evidence cannot be shown to anyone — there is no way to answer "what was proven about this revision?" from off the machine that ran it.

A planned export closes that without weakening the ledger.

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
