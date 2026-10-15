---
title: Verification
---

<DocEyebrow slice="Verification" status="available" />

# Verification

<DocWhat>
`agent-up.json`'s `verification` section maps changed paths to named checks. Running those checks records receipts outside the working tree.

The guard hashes current files against those receipts, so editing after a run leaves the receipt stale. This repository sets `enforcement` to `block`, so `guard` fails until every required check has a matching receipt.
</DocWhat>

<DocCallout>
Run verification before enqueueing commits. Verification never reads the commit queue. If the guard fails, run `agent-up verify run` again; do not edit `enforcement` to get unblocked.
</DocCallout>

<DocSpine>
<DocBeat>Plan the required checks</DocBeat>
<DocBeat>Run them and record receipts</DocBeat>
<DocBeat>Guard before enqueue</DocBeat>
</DocSpine>

<DocContract label="Command">agent-up verify run</DocContract>

## Commands

<DocSteps>
<DocStep title="plan">See which checks the current changes require.</DocStep>
<DocStep title="run">Run every required check and record a receipt.</DocStep>
<DocStep title="run architecture">Re-run one named check after a targeted fix.</DocStep>
<DocStep title="guard">Prove receipts still match the current files.</DocStep>
<DocStep title="export">Write a report of what was proven about this revision. It is evidence to read, not an input to the guard.</DocStep>
<DocStep title="coverage / slices">Read Cobertura reports written by prior test checks. They do not run the suites themselves.</DocStep>
</DocSteps>

The field contract is in the [agent-up.json reference](/docs/configuration/reference#verification-object).

<DocNext href="/docs/commits" title="Commits">
Enqueue after the receipts pass.
</DocNext>
