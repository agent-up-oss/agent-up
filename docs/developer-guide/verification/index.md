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

The field contract is the [agent-up.json reference](/docs/configuration/reference#verification-object). Agent operating rules stay in `AGENTS.md`.

<DocNext href="/developer-guide/commits" title="Commits">
Enqueue only after receipts pass.
</DocNext>
