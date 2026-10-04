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
- Native Windows Server is not a first-class Nix host. Run Server on WSL2 or Linux. There is no second non-Nix capability install path.

## Automation Access

- The MCP automation interface accepts loopback connections only until you turn on `AGENTUP_MCP_REMOTE_ENABLED`. Until then, tools reachable on the Server host are not reachable from anywhere else, including your own second machine or a CI runner.
- With it on, a caller from another address must present a bearer token carrying that endpoint's permission; a request with no token still looks like nothing is there. You supply the issuer and audience, because Agent-Up ships no default endpoint and depends on no particular identity provider.
- Desktop and Mobile are unaffected: they use the REST API, which does support remote connections and authentication.
- Several MCP tools identify their target by an absolute path on the Server's filesystem, so they assume the caller shares it.

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
| MCP access from another machine | Preview — off by default, requires bearer authentication |
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
