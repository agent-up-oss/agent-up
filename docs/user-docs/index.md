---
title: Agent-Up
slug: /
---

# Agent-Up

<DocWhat>
Agent-Up is a local runtime control plane for parallel AI-assisted software development. It is not an application framework, deployment tool, IDE, or production orchestrator.

It manages the running development environment around applications: workspaces, Git worktrees, application processes, port allocation, Docker services, isolated browser profiles, logs, diagnostics, event history, and automation surfaces.
</DocWhat>

<DocCallout kind="warning" label="Development Preview">
Agent-Up is under active development. Packaged artifacts, APIs, configuration, installer behavior, and workflows may change without notice.
</DocCallout>

## What Agent-Up solves

Modern AI-assisted development creates multiple parallel runtimes. Existing tooling usually assumes one developer running one application instance.

<DocSteps>
<DocStep title="Stop colliding runtimes">
Constantly starting and stopping services, Docker collisions, and browser tab sprawl.
</DocStep>
<DocStep title="Keep one source of truth">
The Server owns orchestration. Desktop, Mobile, CLI, and MCP clients stay thin.
</DocStep>
<DocStep title="Leave application source unchanged">
Agent-Up injects ports and environment at launch. Applications do not take an Agent-Up dependency.
</DocStep>
</DocSteps>

<DocFacts label="Clients">
<DocFact label="Desktop">Human workspace chrome</DocFact>
<DocFact label="Mobile">Android, iOS, and the installable PWA</DocFact>
<DocFact label="CLI">agent-up</DocFact>
<DocFact label="MCP">Five named servers, never a shared /mcp</DocFact>
</DocFacts>

<DocCallout kind="warning">
<DocFact label="Packaged">{'http://localhost:5000'}</DocFact>
<DocFact label="Repository">{'http://localhost:5001'}</DocFact>
</DocCallout>

## Getting started

See [Current Limitations](/docs/start/limitations) for the current implementation status of each major area.

<DocSteps>
<DocStep title="Start the Server">
Installed Desktop artifacts run the Server as the local `agent-up-server` service. From the repository root, `dotnet run --project AgentUp.Server` listens on `http://localhost:5001`.
</DocStep>
<DocStep title="Add agent-up.json">
Put it at the root of the repository you want to manage. Only `name` is required.
</DocStep>
<DocStep title="Start the workspace">
From that repository, run `agent-up start --server http://localhost:5001`.
</DocStep>
</DocSteps>

```json
{
  "name": "My App",
  "applications": [
    {
      "name": "Frontend",
      "command": "npm run dev",
      "path": ".",
      "ports": [
        { "variable": "PORT", "defaultPort": 3000 }
      ]
    }
  ]
}
```

When invoking with `dotnet run`, pass CLI arguments after `--`:

```bash
dotnet run --project /path/to/AgentUp.CLI -- start --server http://localhost:5001
```

## Documentation map

Docs are grouped by product slice, not by client.

- [Start](/docs/start/setup) covers downloads, setup, releases, limitations, and the roadmap.
- [Workspaces](/docs/workspaces) is identity, connect, start/stop, and clones.
- [Applications](/docs/applications) is processes, ports, console, and hosted GUI apps.
- [Git](/docs/git) is working-tree review, history, and remotes.
- [Commits](/docs/commits) is the agent proposal queue.
- [Agents](/docs/agents) is the ACP session and subscription sign-in.
- [Browser](/docs/browser) is isolated surfaces and validation.
- [Diagnostics](/docs/diagnostics) is console, health, and audit history.
- [Verification](/docs/verification) is path-rule checks and receipts.
- [Configuration](/docs/configuration) is `agent-up.json`.

Implementation details live in the [Developer Guide](/developer-guide/).
