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

## Hosting-provider state

This slice still reads the local repository. A `forge` capability kind supplies hosting state, and the Git surfaces display it. Display only — the same discipline the surfaces already follow with the queue.

Availability is `none`, `needsCredential`, `unreachable`, `noProposal`, or `ready`. A workspace with no forge enabled stays `none` and shows no hosting UI.

<DocFacts label="Boundaries">
<DocFact label="Desktop">A strip above the change tree, not a new tab</DocFact>
<DocFact label="Mobile">A row on the Git overview, detail on Review</DocFact>
<DocFact label="Empty states">No capability, no credential and no proposal read differently</DocFact>
<DocFact label="Review threads">Link out; Agent-Up is not a review client</DocFact>
</DocFacts>

<DocNext href="/developer-guide/git/review" title="Review tree and diffs">
Change tree, file diffs, and selective commits.
</DocNext>
