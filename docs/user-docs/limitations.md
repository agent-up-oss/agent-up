---
title: Current Limitations
---

# Current Limitations

Agent-Up is an experimental development preview. It is intended for early technical feedback, not production use.

## Release Status

- Source-only execution.
- No stable desktop download.
- Cross-platform installers are preliminary and are being moved into testable installer projects.
- No automatic updater.
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
| Application process launch | Experimental |
| Docker service definitions | Experimental |
| Port allocation | Implemented |
| Desktop workspace list | Implemented |
| Desktop application tabs | Implemented |
| Console/log display | Implemented |
| Browser profile isolation | Experimental |
| Diagnostics | Experimental |
| Event recording | Planned |
| Playwright generation | Planned |
| MCP tools | In progress |
| CLI | Experimental |
| Cross-platform packaging | In progress |
| Health monitoring | Planned |

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
