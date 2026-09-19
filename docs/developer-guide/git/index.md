---
title: Git
---

<DocEyebrow slice="Git" status="available" />

# Git

<DocWhat>
Git is the human review-and-commit surface for a workspace worktree: change tree, per-file diffs, selective commits, remotes, and a bounded log.

It is separate from `Commits`, which owns the agent-facing queue. Desktop Commit and Mobile Review display that queue as read-only.
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

<DocNext href="/developer-guide/git/review" title="Review tree and diffs">
Change tree, file diffs, and selective commits.
</DocNext>
