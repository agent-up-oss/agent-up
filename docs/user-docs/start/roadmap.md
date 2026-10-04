---
title: Roadmap
---

# Roadmap

Agent-Up should evolve beyond being a process launcher.

The long-term goal is for Agent-Up to become the runtime operating system for AI-assisted development:

- Git manages source code.
- Docker manages containers.
- The IDE manages editing.
- Agent-Up manages the running development environment.

Developers and AI agents collaborate inside the same live workspace.

## Direction

Future work should deepen the Server-owned runtime model, improve workflow inference from recorded events, enrich diagnostics, and keep MCP as the primary path for AI automation. Desktop WebViews, Mobile WebViews, and the Server headless profile stay isolated; they share a workspace, not a browser session.

One direction follows from that. The evidence a workspace produces should be shareable: a verification receipt proves which checks ran against which exact file contents, and today that proof cannot leave the machine that produced it.

The automation interface is now reachable by a client that is not on the Server host, under authentication, without changing the loopback-only default — a Server on a workstation or home-lab box is as automatable as it is browsable.

Where the MCP interface is exposed beyond loopback, the hostname, TLS and identity provider are chosen by whoever runs the Server. Agent-Up ships no default endpoint and depends on no particular provider.

## Status Labels

| Area | Status |
|---|---|
| Server-owned workspace registry | Implemented |
| Feature-sliced Server, Desktop, and CLI projects | Implemented |
| Source-first CLI workflow | Preview |
| Desktop workspace and application views | Implemented |
| Git review and history | Implemented |
| Per-workspace port allocation | Implemented |
| Docker lifecycle management | Preview |
| Browser profile persistence | Preview |
| Diagnostics | Preview |
| Health monitoring | Preview |
| MCP automation interface | Preview (contracts may change) |
| Event recording | Experimental |
| Validation flows and Playwright export | Preview |
| Cross-platform packaging | Preview |
| Stable installers | Preview |
| Broad platform support | Preview |
| Authenticated MCP access from another machine | Preview |
| Workspace-id addressing for every MCP tool | Planned |
| Verification receipt export | Planned |
| Repository hosting capability modules | Planned |
| Workspaces started at a specific revision | Planned |
| Workflow inference from events | Planned |
| Service-aware updates | Planned |

See [Current Limitations](/docs/start/limitations) for the practical release status.
