---
title: Current Limitations
---

# Current Limitations

Agent-Up is an experimental development preview. It is intended for early technical feedback, not production use.

## Release Status

- Development-preview packages exist for Windows, macOS, Ubuntu, and NixOS. See [Downloads](/docs/start/downloads).
- Those packages are not a stable update channel.
- There is no automatic updater.
- No `v1.0` release.
- Public contracts may break without notice.

## Platform Status

- The current development setup has been verified on NixOS first.
- Agent-Up may work on additional platforms, but they should be treated as unverified until tested.
- Preliminary NixOS support exists through `shell.nix` and `run-desktop.sh`.

## Feature Status

| Area | Status |
|---|---|
| Workspace registration | Implemented |
| Server-owned workspace state | Implemented |
| Application process launch | Preview |
| Docker service definitions | Preview |
| Port allocation | Implemented |
| Desktop workspace list | Implemented |
| Desktop application tabs | Implemented |
| Console/log display | Implemented |
| Git review and history | Implemented |
| Browser profile isolation | Preview |
| Diagnostics | Preview |
| Health monitoring | Preview |
| Event recording | Experimental |
| Validation flows and Playwright export | Preview |
| MCP tools | Preview (contracts may change) |
| CLI | Preview |
| Cross-platform packaging | Preview |

## API and Configuration Stability

- `agent-up.json` may change.
- REST endpoints may change.
- MCP interfaces may change.
- Data persistence and migration guarantees are not stable.
- Error handling is still being hardened.

## Security and Support

- Agent-Up is not security hardened for production use.
- There is no commercial support commitment.
- Response times for issues and security reports are best effort.
- Known failures should be documented instead of hidden.
