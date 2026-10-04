---
title: Git
---

<DocEyebrow slice="Git" status="available" />

# Git

<DocWhat>
Git is the human review-and-commit surface for a workspace worktree: change tree, per-file diffs, selective commits, remotes, and a bounded log.

It is separate from `Commits`, which owns the agent-facing queue. Desktop Git and Mobile Review display that queue as read-only.
</DocWhat>

<DocMeta
  owner="AgentUp.Server Git slice"
  tests="AgentUp.Server.Tests/Features/Git/"
  rest={'/api/workspaces/{workspaceId}/git/*'}
/>

<DocSpine>
<DocBeat>Read the change tree</DocBeat>
<DocBeat>Commit or discard selected paths</DocBeat>
<DocBeat>Use remotes and history when the user asked</DocBeat>
</DocSpine>

<DocContract label="Route">{'GET /api/workspaces/{workspaceId}/git/changes'}</DocContract>

## Planned: hosting-provider state

This slice reads the local repository. It knows nothing about where that repository is hosted, so a reviewer has to leave for a browser to find out whether checks passed, whether the base branch conflicts, or whether anyone objected.

A planned `forge` capability kind supplies that state, and the Git surfaces display it. Display only — the same discipline the surfaces already follow with the queue.

<DocFacts label="Boundaries">
<DocFact label="Desktop">A strip above the change tree, not a new tab</DocFact>
<DocFact label="Mobile">A row on the Git overview, detail on Review</DocFact>
<DocFact label="Empty states">No capability, no credential and no proposal read differently</DocFact>
<DocFact label="Review threads">Link out; Agent-Up is not a review client</DocFact>
</DocFacts>

A workspace with no forge enabled shows no hosting UI at all.

<DocNext href="/developer-guide/git/review" title="Review tree and diffs">
Change tree, file diffs, and selective commits.
</DocNext>
