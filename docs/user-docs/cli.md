---
title: CLI
---

# AgentUp.CLI

`AgentUp.CLI` is a developer convenience layer over the Server.

Technology:

- .NET Console

## Invoking with dotnet run

The CLI is run with `dotnet run`. Pass CLI arguments after `--`:

```bash
dotnet run --project AgentUp.CLI -- <command> [--server <url>]
```

The server URL defaults to `$AGENTUP_SERVER_URL` or `http://localhost:5000` when neither is set. Pass `--server` explicitly if the server is listening somewhere else, or set the environment variable. When running Server from the repository launch profile, use `http://localhost:5001`.

```bash
export AGENTUP_SERVER_URL=http://localhost:5001
```

## Commands

### start

Searches the current directory and its parents for `agent-up.json`, then pushes the workspace and application definitions to the server. The directory containing `agent-up.json` is the workspace root, even when the command is invoked from a nested directory that is not itself a Git repository. The command fails only when no `agent-up.json` exists anywhere in that directory chain.

`start` works like `npm install` — running it is what makes the workspace exist on the server. If the workspace has never been started, it does not exist. Running `start` again from the same workspace updates the existing workspace in place.

```bash
dotnet run --project AgentUp.CLI -- start --server http://localhost:5001
```

The workspace identity is the directory containing `agent-up.json`. Git metadata is optional: when that directory is not a Git repository, the workspace is still registered and its branch is shown as `not on a git branch`.

### list

Lists all workspaces currently known to the server.

```bash
dotnet run --project AgentUp.CLI -- list --server http://localhost:5001
```

### clear

Stops and removes all workspaces currently known to the server.

```bash
dotnet run --project AgentUp.CLI -- clear --server http://localhost:5001
```

### status

Shows the state of the workspace in the current directory.

```bash
dotnet run --project AgentUp.CLI -- status --server http://localhost:5001
```

### diagnostics

Shows the current workspace's process and health state, recent application logs, and relevant JavaScript, network, and browser errors. Diagnostic entries are marked `active` or `resolved` and identify the affected application or browser session when that context is available.

```bash
dotnet run --project AgentUp.CLI -- diagnostics --server http://localhost:5001
```

### auth

Authenticates the CLI with a Server that requires the admin password. Tokens are stored locally per server URL and attached automatically to later workspace commands.

`auth` commands always require an explicit `--server` argument. They do not fall back to repository `.env` values or `AGENTUP_SERVER_URL`, so a checked-out `.env` cannot redirect your password to another host.

When a workspace command receives `401 Unauthorized`, the CLI prints:

```text
use: auth login --server
```

Log in with:

```bash
dotnet run --project AgentUp.CLI -- auth login --server http://localhost:5001 --password "$AGENTUP_ADMIN_PASSWORD"
```

Omit `--password` to enter the admin password interactively. Use `auth status` to check whether authentication is required and configured, and `auth logout` to remove the stored token for the selected server.

### commits

Manages a local vertical-slice commit staging queue. The `commits` subcommand has no Server dependency — it operates entirely on the local working tree and a queue file stored in the platform config directory, scoped to the current Git repository.

Mutating queue commands are blocked while Git has an active merge, rebase, cherry-pick, revert, or bisect in progress. Finish or abort that Git operation before enqueueing, editing, clearing, or staging queued entries.

The queue file is never edited directly. Agents and developers interact with it exclusively through the CLI.

#### commits enqueue

Adds a proposed commit entry to the queue. Intended for coding agents that modify multiple vertical slices in a single task.

```bash
agentup commits enqueue \
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

`--tests` is merged with any build and test commands `agent-up.json`'s `commits` configuration resolves for `--files`, including commands for projects that transitively depend on a touched project. See the [agent-up.json reference](agent-up-json-reference.md#commits-object). When `agent-up.json` has no `commits` configuration, only the explicitly passed `--tests` are recorded.

#### commits status

Shows the current queue. Warns about modified files in the working tree that are not assigned to any queued entry.

```bash
agentup commits status
```

Use `agentup commits status --format json` for integrations that need the queued entry count or active Git operation state.

#### commits changes

Shows working-tree files with queue assignment information. Use this instead of scripting around `git status`, `git ls-files`, or `find`.

```bash
agentup commits changes
agentup commits changes --format json
```

#### commits inspect

Shows one queued entry by index or ID.

```bash
agentup commits inspect 1
agentup commits inspect 1 --patch
```

#### commits edit

Temporarily applies one queued entry back into the working tree so it can be changed safely.

```bash
agentup commits edit begin 1
# modify files
agentup commits edit save
```

The working tree must be clean before `edit begin`. `edit save` rejects changes outside that entry's file list. Add same-slice files explicitly before saving:

```bash
agentup commits files 1 --add path/to/new-file.cs
```

Use `agentup commits edit abort` to restore the working tree and keep the original queued patch.

#### commits message, tests, files

Updates queued entry metadata without editing the queue file directly.

```bash
agentup commits message 1 --message "fix(cli): harden commit queue editing"
agentup commits tests 1 --set "dotnet test AgentUp.CLI.Tests"
agentup commits files 1 --remove old-file.cs
```

Files can only belong to one queued entry at a time.

#### commits remove and restore

Archives a queued entry without staging it. Archived entries can be restored by ID.

```bash
agentup commits remove 1
agentup commits restore <entry-id>
```

#### commits guard

Fails while queued entries, active edit sessions, staged changes, or unassigned working-tree changes remain.

```bash
agentup commits guard
agentup commits guard --format json
```

#### commits next

`commits next` is developer-only. Agents should stop after `commits status` or `commits guard`; they must not stage or pop queued entries.

Stages the files for the first queued entry using `git add`, pops that entry from the queue, and prints the suggested `git commit` command. Run after reviewing the staged changes.

```bash
agentup commits next
# then: git commit -m "<message from output>"
```

Use `agentup commits next --format json` for integrations that need the staged entry's commit message. Blocked results use structured JSON with `staged: false`, `blocked: true`, and a human-readable `message`.

#### commits clear

Archives all entries from the queue without staging anything.

```bash
agentup commits clear
```

## State Ownership

The CLI owns no state. It should not perform orchestration, port allocation, process management, browser control, or diagnostics collection itself.

## Relationship to MCP

MCP is the primary automation interface. The CLI is a human-friendly wrapper around server capabilities.
