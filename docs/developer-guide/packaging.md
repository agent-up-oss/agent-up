---
title: Packaging And Installers
---

# Packaging And Installers

Packaged installations install Agent-Up as three user-visible components backed by one Server-owned runtime:

- `AgentUp.Server` runs as the local `agent-up-server` service.
- `AgentUp.CLI` is available globally as `agent-up`.
- `AgentUp.Desktop` is installed in the native application location.

Installer and packaging behavior is product behavior and must be testable. Shared installer planning, payload, adapter, progress, validation, and platform install contracts live in `AgentUp.Installers`, with tests in `AgentUp.Installers.Tests`. The guided installer UX lives in `AgentUp.InstallerApp`, with Avalonia headless tests in `AgentUp.InstallerApp.Tests` and native-display flow tests in `AgentUp.Tests`. Release artifact staging and native tool orchestration lives in `AgentUp.Packaging`, with tests in `AgentUp.Packaging.Tests`. Shared package and installed-service smoke validation lives in `AgentUp.PackageSmoke`, with tests in `AgentUp.PackageSmoke.Tests`. Native package assets live under `packaging/` and should consume shared installer contracts instead of growing untested platform-only logic.

## Ownership

`AgentUp.Installers` owns installer contracts and deterministic planning:

- Docker prerequisite classification.
- Component selection and install summary state.
- Bundled and online release payload selection.
- Platform adapter contracts for native execution, elevation, progress, validation, and rollback.
- PATH add/remove planning.
- Post-install validation results.
- Uninstall-mode planning.

`AgentUp.InstallerApp` owns the shared guided installer:

- Welcome, license, prerequisite, Docker, component, location, server configuration, payload, summary, progress, and completion pages.
- Offline install from bundled payload by default.
- Optional online update from release metadata when available.
- Adapter-driven elevation only when privileged native operations are required.

Ubuntu, macOS, and Windows have real installer adapters behind the guided app. The app uses the real adapter by default and requires `AGENTUP_INSTALLER_PAYLOAD_ROOT` to point at a payload root containing `desktop`, `server`, and `cli` directories. Tests and local non-privileged flow checks opt into the fake adapter with `AGENTUP_INSTALLER_FAKE=1`.

On Ubuntu, the default adapter installs the staged Desktop, Server, and CLI payload into `/opt/agent-up`, registers `agent-up-server.service`, creates `/usr/bin/agent-up`, writes the desktop launcher, and validates the installed state through systemd and a fresh-shell CLI lookup.

On macOS, the default adapter installs the staged Desktop bundle into `/Applications/Agent-Up.app`, installs Server and CLI payloads into native system locations, registers the `dev.agent-up.server` launchd service, creates `/usr/local/bin` symlinks, and validates the installed state through `launchctl` and a fresh-shell CLI lookup.

On Windows, the default adapter installs the staged Desktop, Server, and CLI payload under `Program Files\Agent-Up`, registers and starts the `agent-up-server` Windows Service with restart policy, writes the CLI shim, adds the installer-managed `bin` directory to machine `PATH` without duplicating it, creates the Start Menu shortcut, and validates the installed state through `sc.exe` and a fresh-shell CLI lookup.

`AgentUp.Packaging` owns release artifact build orchestration:

- Component publish plans and prebuilt payload consumption.
- Native package staging layouts.
- Package metadata generation.
- Native packaging tool invocation.
- Artifact path and naming rules.

CI uses prebuilt payload mode: the Ubuntu build job publishes single-file self-contained Desktop, Server, CLI, `AgentUp.Packaging`, and `AgentUp.PackageSmoke` artifacts for each release runtime, uploads one short-lived GitHub Actions artifact per platform job, and native package jobs download only their own runtime slice before passing `--payload-root` to package those exact payloads. Native package jobs should delete the consumed CI-transfer artifact after download. Native package jobs should not restore, build, or broadly test product .NET projects; they should only run native packaging tools and package/installer smoke validation.

Final release publishing uses the MinIO `mc` client against the public release bucket. The release job downloads the short-lived package artifacts from GitHub Actions, deletes them immediately, validates the complete artifact set, writes `manifest.json` and `checksums.sha256`, then publishes immutable `agent-up/releases/{version}/` objects and mutable `agent-up/latest/` objects. Required release secrets are `AGENTUP_RELEASE_BUCKET`, `AGENTUP_RELEASE_S3_ENDPOINT`, `AGENTUP_RELEASE_S3_ACCESS_KEY`, and `AGENTUP_RELEASE_S3_SECRET_KEY`.

`AgentUp.PackageSmoke` owns smoke validation:

- Artifact discovery and extraction.
- Platform adapter checks for Desktop, Server, CLI, service, launcher, and uninstall metadata.
- A shared `package-smoke.env` handoff that reports package-local Server and CLI paths back to CI scripts.
- Native install, service readiness, installed CLI workspace registration, diagnostics, and uninstall cleanup for installed-service smoke tests.
- Guided installer flow validation through `validate-installer-flow`; this uses the default real adapter unless `AGENTUP_INSTALLER_FAKE=1` is set.

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

Windows packaging uses WiX through `AgentUp.Packaging`. The packaging app generates `Product.wxs`, `Bundle.wxs`, the CLI shim, and the bootstrapper license file, then invokes `wix build` for `Product.msi` and `Setup.exe` while consuming Windows install metadata, WiX generation, service, PATH, and shortcut contracts from `AgentUp.Installers`. Tests assert the generated WiX service, PATH, shortcut, MSI chain, and exact `wix` command shape with an isolated fake command runner.

macOS packaging is migrating to `Product.pkg` through `AgentUp.Packaging`. The packaging app stages the `.app` bundle, launchd plist, CLI payload, component package roots, package scripts, distribution XML, and `pkgbuild`/`productbuild` command shapes while consuming macOS install metadata, plist generation, and package scripts from `AgentUp.Installers`. Tests assert those generated files and commands on any platform; executing the final Apple packaging tools still requires Darwin.

When any native packaging tool is invoked from `AgentUp.Packaging`, tests should assert the exact command shape with an isolated fake command runner and smoke tests should verify the produced artifact on an appropriate runner.

`AgentUp.Packaging package <platform> <runtime-id> <version> [output-dir] --payload-root <path>` packages an existing payload root containing `desktop`, `server`, and `cli` directories. Without `--payload-root`, the command keeps the local developer fallback of publishing those projects before packaging.

Package smoke scripts must use `AgentUp.PackageSmoke validate-package` for macOS, Windows, and Ubuntu artifact contract checks. Windows package smoke validates the Burn `/layout` extraction and requires `Product.msi` to be present somewhere under the extracted layout, since WiX may choose the MSI subdirectory. Installed-service smoke scripts must use `AgentUp.PackageSmoke validate-installed-service` for native installation, service readiness, installed CLI validation, diagnostics, and uninstall cleanup. Guided installer smoke uses `AgentUp.PackageSmoke validate-installer-flow <platform> <work-dir> [payload-root]`; passing a payload root lets the real platform adapter perform the installer work, while `AGENTUP_INSTALLER_FAKE=1` keeps tests non-privileged. CI uses fake mode for the guided installer flow because it validates the shared flow without taking machine-level install ownership; real native install/service behavior is covered by installed-service smoke. Shell smoke code should stay limited to argument forwarding and runner setup.

The shared installer app is the target user-facing install flow. During migration, native `.pkg`, WiX/MSI/Burn, and `.deb` artifacts may still perform direct native install work, but new behavior should move toward wrapping or launching `AgentUp.InstallerApp` with a bundled release payload.

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
- Changes to `AgentUp.InstallerApp` require Avalonia headless tests in `AgentUp.InstallerApp.Tests`; installer flow behavior that must work on real desktop backends also requires native-display coverage in `AgentUp.Tests`.
- Native package changes require `AgentUp.Packaging.Tests` coverage for generated metadata/tool calls and package smoke updates when the installed contract changes.
- Package and installed-service smoke validation changes require focused tests in `AgentUp.PackageSmoke.Tests`.
- Nix wrapper changes require tests that pin the wrapper and shell contract.
- Platform smoke tests remain the integration coverage for services, package managers, PATH, and launcher registration.

Prefer feature-sliced tests under `AgentUp.Installers.Tests/Features/` that match the owning installer feature.
