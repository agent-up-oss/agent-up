---
title: Packaging And Installers
---

# Packaging And Installers

Packaged installations install Agent-Up as three user-visible components backed by one Server-owned runtime:

- `AgentUp.Server` runs as the local `agent-up-server` service.
- `AgentUp.CLI` is available globally as `agent-up`.
- `AgentUp.Desktop` is installed in the native application location.

Installer and packaging behavior is product behavior and must be testable. Shared installer planning and validation logic lives in `AgentUp.Installers`, with tests in `AgentUp.Installers.Tests`. Release artifact staging and native tool orchestration lives in `AgentUp.Packaging`, with tests in `AgentUp.Packaging.Tests`. Native package assets live under `packaging/` and should consume or mirror the shared contract instead of growing untested platform-only logic.

## Ownership

`AgentUp.Installers` owns installer contracts and deterministic planning:

- Docker prerequisite classification.
- Component selection and install summary state.
- PATH add/remove planning.
- Post-install validation results.
- Uninstall-mode planning.

`AgentUp.Packaging` owns release artifact build orchestration:

- Component publish plans.
- Native package staging layouts.
- Package metadata generation.
- Native packaging tool invocation.
- Artifact path and naming rules.

## Nix Packaging Wrappers

Use the platform wrapper scripts when building packages from NixOS or another machine where native package tools should come from Nix:

```bash
./scripts/package-ubuntu.sh linux-x64 0.0.0-local artifacts
./scripts/package-windows.sh win-x64 0.0.0-local artifacts
./scripts/package-macos.sh osx-arm64 0.0.0-local artifacts
```

The wrappers enter a target-specific shell under `packaging/nix/` and then delegate to `scripts/package-release.sh`.

- Ubuntu wrapper provides Debian tooling such as `dpkg-deb` and `fakeroot`.
- Windows wrapper provides cross-packaging helpers where available, such as archive tooling, MSI inspection tools, and signing helpers. WiX is supplied through the pinned local .NET tool manifest at `packaging/windows/dotnet-tools.json`; the wrapper restores that tool and exposes a `wix` shim on PATH before packaging starts.
- macOS wrapper is Darwin-only because Apple packaging, signing, and notarization tools are platform tools: `hdiutil`, `pkgbuild`, `productbuild`, `codesign`, and `notarytool`.

Windows packaging is migrating to WiX through `AgentUp.Packaging`. The packaging app generates `Product.wxs`, `Bundle.wxs`, the CLI shim, and the bootstrapper license file, then invokes `wix build` for `Product.msi` and `Setup.exe`. Tests assert the generated WiX service, PATH, shortcut, MSI chain, and exact `wix` command shape with an isolated fake command runner.

macOS packaging is migrating to `Product.pkg` through `AgentUp.Packaging`. The packaging app stages the `.app` bundle, launchd plist, CLI payload, component package roots, package scripts, distribution XML, and `pkgbuild`/`productbuild` command shapes. Tests assert those generated files and commands on any platform; executing the final Apple packaging tools still requires Darwin.

When any native packaging tool is invoked from `AgentUp.Packaging`, tests should assert the exact command shape with an isolated fake command runner and smoke tests should verify the produced artifact on an appropriate runner.

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
- Native package changes require `AgentUp.Packaging.Tests` coverage for generated metadata/tool calls and package smoke updates when the installed contract changes.
- Nix wrapper changes require tests that pin the wrapper and shell contract.
- Platform smoke tests remain the integration coverage for services, package managers, PATH, and launcher registration.

Prefer feature-sliced tests under `AgentUp.Installers.Tests/Features/` that match the owning installer feature.
