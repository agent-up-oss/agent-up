---
title: Git
---

<DocEyebrow slice="Git" status="available" />

# Git

<DocFocus>
The `Git` slice is the human review-and-commit surface. It is not an escape hatch around `enqueue_commit`.
</DocFocus>

**Owner:** `AgentUp.Server` `Git` slice. Tests live in `AgentUp.Server.Tests/Features/Git/`. REST under `/api/workspaces/{workspaceId}/git/*`. Desktop Commit tab and Mobile Git Review/History are clients.

## What it is

The slice resolves the selected workspace's worktree path and exposes a change tree, per-file diffs, selective commits, remotes, and a bounded log. The provider runs Git through an allowlisted operation set with `ProcessStartInfo.ArgumentList`, rejects pathspec magic, and always passes `--` before user-supplied paths.

<DocSpine>
<DocBeat selected>Read `/git/changes`</DocBeat>
<DocBeat>Commit or discard selected paths</DocBeat>
<DocBeat>Use remotes and history when the user asked</DocBeat>
</DocSpine>

<DocContract>GET /api/workspaces/&#123;workspaceId&#125;/git/changes</DocContract>

This slice is separate from `Commits`. `Commits` owns the agent-facing queue. Desktop Commit and Mobile Review display that queue as read-only from `GET /api/workspaces/{workspaceId}/commit-queue`.

Next in this slice: [Review tree and diffs](/developer-guide/git/review).
