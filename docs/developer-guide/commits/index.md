---
title: Commits
---

<DocEyebrow slice="Commits" status="preview" />

# Commits

<DocFocus>
Agents mutate the queue only through `/mcp/commits`. Desktop Commit and Mobile Review display it; they do not mutate it.
</DocFocus>

**Owner:** `AgentUp.Server` `Commits` slice. Tests live in `AgentUp.Server.Tests/Features/Commits/`. MCP: `/mcp/commits`. Human read model: `GET /api/workspaces/{workspaceId}/commit-queue`.

## What it is

When `commits.enabled` is true, enqueue records a Git-backed proposal stack in a managed worktree without moving the developer's branch. Without that opt-in the legacy independent-patch queue remains. Verification never reads this queue.

<DocSpine>
<DocBeat selected>Call `guard_commits` before new work</DocBeat>
<DocBeat>Enqueue one vertical slice</DocBeat>
<DocBeat>Continue at `queueWorktreePath` when the Git-backed queue is enabled</DocBeat>
</DocSpine>

<DocFork question="Which queue?">
<DocForkPath title="Proposal queue" open>

`commits.enabled` is true. The first enqueue restores the developer worktree after creating the proposal worktree; subsequent changes remain committed and checked out at the proposal tip.

</DocForkPath>
<DocForkPath title="Legacy local queue">

Legacy `enqueue_commit` restores tracked files after saving an independent patch. `commits next` remains CLI-only developer-owned review work.

</DocForkPath>
</DocFork>

<DocContract>enqueue_commit</DocContract>

Next in this slice: [Merge queue assessment](/developer-guide/commits/merge-queue-assessment) (not the current contract).

## Commits MCP

`/mcp/commits` exposes Streamable HTTP and legacy SSE at `/mcp/commits/sse` plus `/mcp/commits/message`. It owns only commit queue tools and exposes no workspace resources.

- `enqueue_commit`: when `commits.enabled` is true, runs the required Verification checks, records the selected delta as the next Git commit in a Server-managed proposal worktree, leaves the developer branch unchanged, and returns the worktree path where the agent must continue dependent work. Without that opt-in it retains the legacy independent-patch behavior during migration.
- `enqueue_review_fix_commit`: saves one review issue violation fix with a required `reviewIssueId`; do not combine multiple review issues in one entry.
- `get_commits_status`: returns queued entries, unassigned modified files, any active commit edit session, and active Git operation state.
- `guard_commits`: returns the managed `continueWorktreePath` when a dependent proposal queue already exists, allowing later tasks to build on its tip. Legacy queued entries, active edit sessions, staged changes, unassigned modified files, and active Git merge/rebase/cherry-pick/revert/bisect operations block work.
- `get_commit_changes`: returns working-tree files with queue assignment information.
- `inspect_commit`: returns one queued entry, optionally including the saved patch.
- `update_commit_message`, `add_commit_files`, `remove_commit_files`: update queued entry metadata and file assignment.
- `remove_commit`, `restore_commit`, `clear_commits`: archive, restore, or clear queued entries.
- `begin_commit_edit`, `save_commit_edit`, `abort_commit_edit`: safely edit an existing queued patch.

Human clients read the same authoritative state through `GET /api/workspaces/{workspaceId}/commit-queue`. Desktop and Mobile display the returned ordered entries and verification states; MCP agents use `get_commits_status` and must continue at the returned managed worktree path after the first Git-backed enqueue.

MCP enqueue operations require conventional commit messages scoped to the queued slice, such as `fix(Commits): validate queue metadata`. When the Server recognizes feature-sliced paths under `Features/<Slice>/`, enqueue operations reject entries that span multiple slices or whose slice label does not match the recognized slice.

Agents should follow the default prefix policy from `get_agent_up_context`. Cross-slice guidance or documentation updates should be queued in a separate guidance/docs entry.

Mutating commit queue operations are blocked while Git reports an active merge, rebase, cherry-pick, revert, or bisect.

## Commit queue reminders

MCP has no server-side lifecycle callback for when an agent finishes a turn. Agents should call `guard_commits` before starting a new coding task. A successful response with `continueWorktreePath` directs the ACP task to the managed proposal tip.

Claude Code installations can surface commit queue reminders with a client-side `Stop` hook that runs `agent-up commits guard`.
