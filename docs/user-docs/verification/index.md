---
title: Verification
---

<DocEyebrow slice="Verification" status="available" />

# Verification

<DocFocus>
Run verification before enqueueing commits. Verification never reads the commit queue.
</DocFocus>

## What it is

`agent-up.json`'s `verification` section maps changed paths to named checks. Receipts live outside the working tree. The guard hashes current files against those receipts, so editing after a run leaves the receipt stale.

<DocSpine>
<DocBeat selected>Plan the required checks</DocBeat>
<DocBeat>Run them and record receipts</DocBeat>
<DocBeat>Guard before enqueue</DocBeat>
</DocSpine>

<DocContract>agent-up verify run</DocContract>

```bash
agent-up verify plan
agent-up verify run
agent-up verify run architecture
agent-up verify guard
agent-up verify coverage
agent-up verify slices
```

`coverage` and `slices` read Cobertura reports written by prior test checks; they do not run the suites themselves.

The field contract is in the [agent-up.json reference](/docs/configuration/reference#verification-object).

Next in this slice: [Commits](/docs/commits) after the receipts pass.
