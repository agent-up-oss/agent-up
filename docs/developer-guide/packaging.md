---
title: Packaging And Installers
---

# Packaging And Installers

Packaged installations install Agent-Up as three user-visible components backed by one Server-owned runtime:

- `AgentUp.Server` runs as the local `agent-up-server` service.
- `AgentUp.CLI` is available globally as `agent-up`.
- `AgentUp.Desktop` is installed in the native application location.

Installer behavior is product behavior and must be testable. Shared installer planning and validation logic lives in `AgentUp.Installers`, with tests in `AgentUp.Installers.Tests`. Native package assets live under `packaging/` and should consume or mirror the shared contract instead of growing untested platform-only logic.

## Ownership

`AgentUp.Installers` owns installer contracts and deterministic planning:

- Docker prerequisite classification.
- Component selection and install summary state.
- PATH add/remove planning.
- Post-install validation results.
- Uninstall-mode planning.

Platform packaging owns native registration:

| Platform | Native package | Service | CLI target | Desktop target |
|---|---|---|---|---|
| Windows | `Setup.exe` plus `Product.msi` | Windows Service | application `bin` directory on PATH | Start Menu and Apps & Features |
| macOS | `Product.pkg` components | `launchd` | `/usr/local/bin/agent-up` | `/Applications/Agent-Up.app` |
| Ubuntu | `agent-up.deb` | `systemd` | `/usr/bin/agent-up` | `.desktop` launcher |

## Validation Contract

An installation is successful only when validation can prove:

- Docker status has been checked and clearly reported.
- `agent-up --version` works from the installed CLI.
- The Server service is registered and running.
- Desktop exists in the native application location.
- CLI, Server, Desktop, and installer metadata report the same product version where the platform exposes that metadata.
- Uninstall removes installer-managed binaries, service registration, launcher entries, PATH entries, and package metadata without deleting user data by default.

Docker must not be installed silently. If Docker is unavailable, the installer reports whether Docker is missing, stopped, inaccessible, or below the minimum supported version.

## Testing

Installer changes follow the same production/test pairing rule as other projects:

- Changes to `AgentUp.Installers` require focused tests in `AgentUp.Installers.Tests`.
- Native package changes require package smoke updates when the installed contract changes.
- Platform smoke tests remain the integration coverage for services, package managers, PATH, and launcher registration.

Prefer feature-sliced tests under `AgentUp.Installers.Tests/Features/` that match the owning installer feature.
