# Agent-Up JetBrains Plugin

Minimal JetBrains IDE integration for the Agent-Up local commit queue.

## MVP Behavior

- Adds a `Queue: N` action to the commit-message action group and the Tools menu.
- Polls `agent-up commits status --format json` every few seconds while the action is visible.
- Runs `agent-up commits next --format json` when the action is clicked.
- Inserts the returned commit message into the active Commit tool window when available.
- Requests a VCS change-list refresh after `next` completes.

## Build

```bash
cd Plugins/Jetbrains
./gradlew buildPlugin
```

The archive for manual IDE installation is written to `build/distributions/`.

`./gradlew test` uses the configured Java toolchain, so it also works when invoked by the IDE Gradle runner. On NixOS, `./gradlew runIde` and `./gradlew verifyPlugin` re-exec through the local flake's FHS wrapper so downloaded JetBrains IDE runtimes can start. `buildPlugin` runs directly and does not launch an editor.

The CLI executable path and timeouts are configurable under Settings | Tools | Agent-Up.
