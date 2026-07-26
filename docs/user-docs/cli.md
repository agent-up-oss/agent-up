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

The server URL defaults to `$AGENTUP_SERVER_URL` or `http://localhost:5000` when neither is set. Pass `--server` explicitly if the server is listening somewhere else, or set the environment variable:

```bash
export AGENTUP_SERVER_URL=http://localhost:5000
```

## Commands

### start

Reads `agent-up.json` from the current directory and pushes the workspace and application definitions to the server. Works like `npm install` — running it is what makes the workspace exist on the server. If the workspace has never been started, it does not exist. Running `start` again from the same directory updates the existing workspace in place.

```bash
dotnet run --project AgentUp.CLI -- start --server http://localhost:5000
```

The workspace identity is the current directory path. Git metadata is optional: when the directory is not a Git repository, the workspace is still registered and its branch is shown as `not on a git branch`.

### list

Lists all workspaces currently known to the server.

```bash
dotnet run --project AgentUp.CLI -- list --server http://localhost:5000
```

### status

Shows the state of the workspace in the current directory.

```bash
dotnet run --project AgentUp.CLI -- status --server http://localhost:5000
```

### commits

Manages a local vertical-slice commit staging queue. The `commits` subcommand has no Server dependency — it operates entirely on the local working tree and a queue file stored in the platform config directory, scoped to the current Git repository.

The queue file is never edited directly. Agents and developers interact with it exclusively through the CLI.

#### commits enqueue

Adds a proposed commit entry to the queue. Intended for coding agents that modify multiple vertical slices in a single task.

```bash
agentup commits enqueue \
  --slice UbuntuInstallation \
  --message "test(UbuntuInstallation): cover tray autostart boundary" \
  --files AgentUp.Installers.Tests/Features/UbuntuInstallation/Provider/UbuntuInstallerPlatformAdapterTests.cs \
  --tests "dotnet test AgentUp.Installers.Tests --filter UbuntuInstallerPlatformAdapterTests"
```

| Flag | Required | Description |
|---|---|---|
| `--slice` | yes | Logical name for the vertical slice (e.g. `UbuntuInstallation`) |
| `--message` | yes | Conventional commit message for this entry |
| `--files` | yes | One or more file paths to stage (space-separated, until next `--` flag) |
| `--tests` | no | One or more test commands to run before committing (space-separated) |

#### commits status

Shows the current queue. Warns about modified files in the working tree that are not assigned to any queued entry.

```bash
agentup commits status
```

#### commits next

Stages the files for the first queued entry using `git add`, pops that entry from the queue, and prints the suggested `git commit` command. Run after reviewing the staged changes.

```bash
agentup commits next
# then: git commit -m "<message from output>"
```

#### commits clear

Removes all entries from the queue without staging anything.

```bash
agentup commits clear
```

## State Ownership

The CLI owns no state. It should not perform orchestration, port allocation, process management, browser control, or diagnostics collection itself.

## Relationship to MCP

MCP is the primary automation interface. The CLI is a human-friendly wrapper around server capabilities.
