---
title: Commits
---

<DocEyebrow slice="Commits" status="preview" />

# Commits

<DocFocus>
Agents enqueue through MCP. Humans review the queue on Git; they do not mutate it there.
</DocFocus>

## What it is

The agent queue is a **commit queue** when `commits.enabled` is absent (legacy local file) or a **proposal queue** when `commits.enabled` is true (Server-owned Git stack). Desktop Commit and Mobile Review display that queue as read-only. The installed CLI is `agent-up`.

<DocSpine>
<DocBeat selected>Choose the queue shape</DocBeat>
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

<DocContract>agent-up commits enqueue</DocContract>

<DocSurface mcp>Agents use `enqueue_commit`, then `get_commits_status`. They never run `commits next`, `git add`, `git commit`, or `git stash`.</DocSurface>

<DocSurface cli>`commits next` is developer-only. It stages the first queued entry and prints the suggested `git commit` command.</DocSurface>

Next in this slice: [Git Review](/docs/git/review) is where humans see the queue.

## Commands

Mutating queue commands are blocked while Git has an active merge, rebase, cherry-pick, revert, or bisect in progress. Finish or abort that Git operation before enqueueing, editing, clearing, or staging queued entries.

Do not edit the queue file directly. Agents and developers interact with it through the CLI or MCP.

### commits enqueue

Adds a proposed commit entry to the queue. Intended for coding agents that modify multiple vertical slices in a single task.

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

### commits status

Shows the current queue. Warns about modified files in the working tree that are not assigned to any queued entry.

```bash
agent-up commits status
```

Use `agent-up commits status --format json` for integrations that need the queued entry count or active Git operation state.

### commits changes

Shows working-tree files with queue assignment information. Use this instead of scripting around `git status`, `git ls-files`, or `find`.

```bash
agent-up commits changes
agent-up commits changes --format json
```

### commits inspect

Shows one queued entry by index or ID.

```bash
agent-up commits inspect 1
agent-up commits inspect 1 --patch
```

### commits edit

Temporarily applies one queued entry back into the working tree so it can be changed safely.

```bash
agent-up commits edit begin 1
# modify files
agent-up commits edit save
```

The working tree must be clean before `edit begin`. `edit save` rejects changes outside that entry's file list. Add same-slice files explicitly before saving:

```bash
agent-up commits files 1 --add path/to/new-file.cs
```

Use `agent-up commits edit abort` to restore the working tree and keep the original queued patch.

### commits message, tests, files

Updates queued entry metadata without editing the queue file directly.

```bash
agent-up commits message 1 --message "fix(cli): harden commit queue editing"
agent-up commits tests 1 --set "dotnet test AgentUp.CLI.Tests"
agent-up commits files 1 --remove old-file.cs
```

Files can only belong to one queued entry at a time.

### commits remove and restore

Archives a queued entry without staging it. Archived entries can be restored by ID.

```bash
agent-up commits remove 1
agent-up commits restore <entry-id>
```

### commits guard

Fails while queued entries, active edit sessions, staged changes, or unassigned working-tree changes remain.

```bash
agent-up commits guard
agent-up commits guard --format json
```

### commits next

`commits next` is developer-only. Agents should stop after `commits status` or `commits guard`; they must not stage or pop queued entries.

Stages the files for the first queued entry using `git add`, pops that entry from the queue, and prints the suggested `git commit` command. Run after reviewing the staged changes.

```bash
agent-up commits next
# then: git commit -m "<message from output>"
```

Use `agent-up commits next --format json` for integrations that need the staged entry's commit message. Blocked results use structured JSON with `staged: false`, `blocked: true`, and a human-readable `message`.

### commits clear

Archives all entries from the queue without staging anything.

```bash
agent-up commits clear
```

## State ownership

The CLI owns no runtime or orchestration state. The legacy local commit queue file is the documented exception until `commits.enabled` migration finishes. MCP is the primary automation interface.
