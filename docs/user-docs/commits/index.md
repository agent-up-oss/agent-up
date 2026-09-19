---
title: Commits
---

<DocEyebrow slice="Commits" status="preview" />

# Commits

<DocWhat>
The agent queue is a **commit queue** when `commits.enabled` is absent, or a **proposal queue** when `commits.enabled` is true.

Desktop Commit and Mobile Review display that queue as read-only. The installed CLI is `agent-up`.
</DocWhat>

<DocCallout>
Agents enqueue through MCP. Humans review the queue on Git; they do not mutate it there.
</DocCallout>

<DocSpine>
<DocBeat>Choose the queue shape</DocBeat>
<DocBeat>Enqueue a vertical-slice entry</DocBeat>
<DocBeat>A human runs `agent-up commits next`</DocBeat>
</DocSpine>

<DocFork question="Which queue?">
<DocForkPath title="Proposal queue" open>

`commits.enabled` is true. Enqueue records a Server-owned proposal in a managed worktree. Agents continue at the returned worktree path.

</DocForkPath>
<DocForkPath title="Legacy local queue">

The setting is absent. This is a local queue file in the platform config directory, scoped to the current Git repository, with no Server dependency.

</DocForkPath>
</DocFork>

<DocContract label="Command">agent-up commits enqueue</DocContract>

<DocSurfaces>
<DocSurface mcp>Agents use `enqueue_commit`, then `get_commits_status`. They never run `commits next`, `git add`, `git commit`, or `git stash`.</DocSurface>
<DocSurface cli>`commits next` is developer-only. It stages the first queued entry and prints the suggested `git commit` command.</DocSurface>
</DocSurfaces>

## Commands

<DocCallout kind="warning">
Mutating queue commands are blocked while Git has an active merge, rebase, cherry-pick, revert, or bisect. Finish or abort that Git operation first.
</DocCallout>

Do not edit the queue file directly. Agents and developers interact with it through the CLI or MCP.

<DocSteps>
<DocStep title="commits enqueue">
Adds a proposed commit entry. Intended for coding agents that modify multiple vertical slices in a single task.
</DocStep>
<DocStep title="commits status">
Shows the current queue. Warns about modified files that are not assigned to any queued entry. `--format json` is for integrations.
</DocStep>
<DocStep title="commits changes">
Shows working-tree files with queue assignment. Use this instead of scripting around `git status`.
</DocStep>
<DocStep title="commits inspect">
Shows one queued entry by index or ID. `--patch` includes the saved patch.
</DocStep>
<DocStep title="commits edit">
Applies one queued entry back into a clean working tree. `edit save` rejects changes outside that entry's file list. `edit abort` keeps the original patch.
</DocStep>
<DocStep title="commits message, tests, files">
Updates queued entry metadata. Files can only belong to one queued entry at a time.
</DocStep>
<DocStep title="commits remove and restore">
Archives a queued entry without staging it. Restore by ID.
</DocStep>
<DocStep title="commits guard">
Fails while queued entries, active edit sessions, staged changes, or unassigned working-tree changes remain.
</DocStep>
<DocStep title="commits next">
Developer-only. Stages the first queued entry, pops it, and prints the suggested `git commit` command.
</DocStep>
<DocStep title="commits clear">
Archives all entries without staging anything.
</DocStep>
</DocSteps>

### commits enqueue

```bash
agent-up commits enqueue \
  --slice UbuntuInstallation \
  --message "fix(UbuntuInstallation): cover tray autostart boundary" \
  --files AgentUp.Installers.Tests/Features/UbuntuInstallation/Provider/UbuntuInstallerPlatformAdapterTests.cs \
  --tests "dotnet test AgentUp.Installers.Tests --filter UbuntuInstallerPlatformAdapterTests"
```

| Flag | Required | Description |
|---|---|---|
| `--slice` | yes | Logical name for the vertical slice (e.g. `UbuntuInstallation`) |
| `--message` | yes | Conventional commit message scoped to this entry's slice |
| `--files` | yes | One or more file paths to stage (space-separated, until next `--` flag) |
| `--tests` | no | One or more test commands to run before committing (space-separated) |

`--tests` is merged with any build and test commands `agent-up.json`'s `commits` configuration resolves for `--files`, including commands for projects that transitively depend on a touched project. See the [agent-up.json reference](/docs/configuration/reference#commits-object). When `agent-up.json` has no `commits` configuration, only the explicitly passed `--tests` are recorded.

### commits next

Agents should stop after `commits status` or `commits guard`; they must not stage or pop queued entries.

```bash
agent-up commits next
# then: git commit -m "<message from output>"
```

Use `agent-up commits next --format json` for integrations that need the staged entry's commit message. Blocked results use structured JSON with `staged: false`, `blocked: true`, and a human-readable `message`.

## State ownership

The CLI owns no runtime or orchestration state. The legacy local commit queue file is the documented exception until `commits.enabled` migration finishes. MCP is the primary automation interface.

<DocNext href="/docs/git/review" title="Git Review">
Where humans see the queue.
</DocNext>
