---
title: Releases
---

# Releases

Agent-Up publishes development-preview builds for Windows, macOS, Ubuntu, and NixOS.

Use [Downloads](./downloads.md) for the current stable download links and platform install commands.

## Published Artifacts

| Platform | Artifact |
|---|---|
| Windows | Installer `.exe` |
| macOS Apple Silicon | `.pkg` package |
| macOS Intel | `.pkg` package |
| Ubuntu | Debian `.deb` package |
| NixOS | Flake package-set tarball |

Packaged Desktop installations include the local Agent-Up Server service and the CLI. Desktop, CLI, and MCP clients connect to the local Server at `http://localhost:5000` unless `AGENTUP_SERVER_URL` points somewhere else.

## Stable URLs

Release files are published under two URL shapes:

- `agent-up/releases/{version}/` for immutable versioned artifacts.
- `agent-up/latest/` for the latest release.

Use the `latest` URLs for manual installation unless you need to pin an exact version.

## Updates

Automatic background updates are not available yet. To update, download the newest artifact for your platform and run the platform installer or package manager again.

Uninstall and upgrade behavior should preserve user-generated workspace data unless an explicit data-removal option is selected.
