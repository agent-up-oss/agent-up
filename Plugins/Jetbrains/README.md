# Agent-Up Commit Queue

JetBrains IDE integration for the local Agent-Up commit queue.

The plugin adds an Agent-Up button to the Commit tool window. It watches the local
`agent-up commits` queue and can stage the next queued vertical-slice commit
without leaving the IDE.

## Requirements

- Agent-Up CLI installed as `agent-up`, or a custom executable configured under
  Settings | Tools | Agent-Up.
- A local Git repository opened as the IDE project.
- An Agent-Up CLI version that supports:
  - `agent-up commits status --format json`
  - `agent-up commits next --format json`

## Usage

- Open the Commit tool window.
- The Agent-Up logo appears in the commit-message action area.
- Grey icon: the queue is empty.
- Red icon: the CLI is unavailable or returned an error.
- Normal icon: queued entries exist.
- Click the icon to run `agent-up commits next --format json`.
- The plugin refreshes Git changes and inserts the queued commit message into
  the commit-message field.

The plugin does not create commits. It only stages the next queued entry so you
can review the diff and commit manually inside the IDE.

The CLI executable path, polling interval, and timeouts are configurable under
Settings | Tools | Agent-Up. During local development, the executable can be a
command such as:

```bash
dotnet run --project /path/to/AgentUp.CLI/AgentUp.CLI.csproj
```

## Build

```bash
cd Plugins/Jetbrains
./gradlew buildPlugin
```

The archive for manual IDE installation is written to `build/distributions/`.

`./gradlew test` uses the configured Java toolchain, so it also works when invoked by the IDE Gradle runner. On NixOS, `./gradlew runIde` and `./gradlew verifyPlugin` re-exec through the local flake's FHS wrapper so downloaded JetBrains IDE runtimes can start. `buildPlugin` runs directly and does not launch an editor.
