---
title: Packaging And Installers
---

# Packaging And Installers

Packaged installations install Agent-Up as three user-visible components backed by one Server-owned runtime:

- `AgentUp.Server` runs as the local `agent-up-server` service.
- `AgentUp.CLI` is available globally as `agent-up`.
- `AgentUp.Desktop` is installed in the native application location.

Installer and packaging behavior is product behavior and must be testable. Shared installer planning, payload, adapter, progress, validation, per-component install/update/uninstall/repair, and platform install contracts live in `AgentUp.Installers`, with tests in `AgentUp.Installers.Tests`. The InstallerApp dashboard UX lives in `AgentUp.InstallerApp`, with Avalonia headless tests in `AgentUp.InstallerApp.Tests` and native-display flow tests in `AgentUp.Tests`. Release artifact staging and native tool orchestration lives in `AgentUp.Packaging`, with tests in `AgentUp.Packaging.Tests`. Shared package and installed-service smoke validation lives in `AgentUp.PackageSmoke`, with tests in `AgentUp.PackageSmoke.Tests`. Native package assets live under `packaging/` and should consume shared installer contracts instead of growing untested platform-only logic.

## Ownership

`AgentUp.Installers` owns installer contracts and deterministic planning:

- Docker prerequisite classification.
- Component selection and install summary state.
- Bundled and online release payload selection.
- Platform adapter contracts for native execution, elevation, progress, validation, and rollback.
- PATH add/remove planning.
- Post-install validation results.
- Uninstall-mode planning.

`AgentUp.InstallerApp` owns the shared installer dashboard:

- Independent Desktop, Server, and CLI management cards.
- Install, update, uninstall, repair, status, and per-card progress UI.
- Installed capability-module grid with add-module and standardized version-management pages.
- Official capability catalog loading from the bundled catalog, with `AGENTUP_CAPABILITY_CATALOG_URL` available for tests and alternate release channels.
- Offline install from bundled payload by default.
- Optional online update from release metadata when available.
- Adapter-driven elevation only when privileged native operations are required.
- Noninteractive installer operations through `AgentUp.InstallerApp --smoke-installer-operations --payload-root <payload-root>` for smoke tests and CI workflows that must exercise the shipped installer executable without driving pointer clicks. This smoke command installs, repairs, updates, and uninstalls `desktop`, `server`, and `cli` individually before running the bundled core install and validation. Focused commands are also available for `--install-core`, `--validate-installed`, and `--install-component`, `--update-component`, `--repair-component`, or `--uninstall-component` with a component target.

Ubuntu, macOS, and Windows have real installer adapters behind the guided app. The app uses the real adapter by default and requires a payload root containing `desktop`, `server`, and `cli` directories. The payload root may come from `AGENTUP_INSTALLER_PAYLOAD_ROOT`, from a bundled offline payload in the installer app, or from a future online payload download. Tests and local non-privileged flow checks opt into the fake adapter with `AGENTUP_INSTALLER_FAKE=1`.

On Ubuntu, the default adapter installs the staged Desktop, Server, and CLI payload into `/opt/agent-up`, registers `agent-up-server.service`, creates `/usr/bin/agent-up`, writes the desktop launcher, and validates the installed state through systemd and a fresh-shell CLI lookup.

On macOS, the default adapter installs the staged Desktop bundle into `/Applications/Agent-Up.app`, installs Server and CLI payloads into native system locations, registers the `dev.agent-up.server` launchd service, creates `/usr/local/bin` symlinks, and validates the installed state through `launchctl` and a fresh-shell CLI lookup.

On Windows, the default adapter installs the staged Desktop, Server, and CLI payload under `Program Files\Agent-Up`, removes any previous `agent-up-server` Windows Service before registering the new one with restart policy, writes the CLI shim, adds the installer-managed `bin` directory to machine `PATH` without duplicating it, creates the Start Menu shortcut, registers uninstall metadata for Apps & Features, and validates the installed state through `sc.exe` and a fresh-shell CLI lookup.

`AgentUp.Packaging` owns release artifact build orchestration:

- Component publish plans and prebuilt payload consumption.
- Native package staging layouts.
- Package metadata generation.
- Native packaging tool invocation.
- Artifact path and naming rules.

All `AgentUp.Packaging` filesystem access must pass through shared path validation in `Shared/Providers/PackagePathValidator` before reading, writing, copying, deleting, or creating directories. Package output directories are repository-relative paths and must remain under the repository root. Prebuilt payload roots may be absolute CI-provided paths or repository-relative paths; repository-relative payload roots are normalized under the repository root.

The packaging command entrypoint builds the project composition root and delegates into feature controllers. Controllers are constructor-injected and thin; packaging services own lifecycle orchestration, and low-level command, filesystem, repository-path, environment, parser, archive, and native-tool behavior stays behind providers. Platform packaging slices may coordinate shared release artifact staging through the ReleaseArtifacts controller, but they must not fetch another slice's services, models, providers, or interfaces directly.

Packaging services must not construct native tool commands such as `dpkg-deb`, `wix`, `pkgbuild`, `productbuild`, `dotnet publish`, package archive expansion, or raw CLI argument parsing. Use capability-named providers such as `DpkgDebPackageTool`, `WindowsWixPackagingTool`, `MacOsPackageTool`, `PackagePublisher`, and `PackageCommandParser`; test exact command shapes at the provider level.

CI uses prebuilt payload mode: the Ubuntu build job restores once per release runtime, then publishes single-file self-contained InstallerApp, Desktop, Server, CLI, `AgentUp.Packaging`, and `AgentUp.PackageSmoke` artifacts for that runtime with restore disabled.

The Ubuntu build uploads one short-lived GitHub Actions artifact per platform job, and native package jobs download only their own runtime slice before passing `--payload-root` to package those exact payloads. Native package jobs should delete the consumed CI-transfer artifact after download. Native package jobs should not restore, build, or broadly test product .NET projects; they should only run native packaging tools and package/installer smoke validation.

Final release publishing uses the MinIO `mc` client against the public release bucket. The release job downloads the short-lived package artifacts from GitHub Actions, deletes them immediately, validates the complete artifact set, writes `manifest.json` and `checksums.sha256`, then publishes immutable `agent-up/releases/{version}/` objects and mutable `agent-up/latest/` objects. Required release secrets are `AGENTUP_RELEASE_BUCKET`, `AGENTUP_RELEASE_S3_ENDPOINT`, `AGENTUP_RELEASE_S3_ACCESS_KEY`, and `AGENTUP_RELEASE_S3_SECRET_KEY`.

`AgentUp.PackageSmoke` owns smoke validation:

- The `SmokeRuns` command entrypoint controller, parser provider, work-directory provider, and validation router.
- Artifact discovery and extraction.
- Platform adapter checks for Desktop, Server, CLI, service, launcher, and uninstall metadata.
- Installed-service capability lifecycle smoke for one .NET app and one Docker app, including capability discovery, workspace launch, individual app stop/start, and workspace stop cleanup.
- A shared `package-smoke.env` handoff that reports package-local Server and CLI paths back to CI scripts.
- Native install, service readiness, installed CLI workspace registration, diagnostics, and uninstall cleanup for installed-service smoke tests.
- Guided installer flow validation through `validate-installer-flow`; this uses the default real adapter unless `AGENTUP_INSTALLER_FAKE=1` is set.

Package smoke command execution must choose from allowlisted command names before creating `ProcessStartInfo`; executable paths must not be passed to the generic process runner. CI scripts may pass artifact, install, and work-directory values into smoke validation, but those values must stay data until a provider has checked them.

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

The generated Nix package-set flake exports both `nixosModules.default` and `homeManagerModules.default`. The NixOS module registers the Server as the system `agent-up-server.service`. The Home Manager module installs Desktop, CLI, and the lookup-only InstallerApp dashboard and, by default, registers a user `agent-up-server.service` so Home Manager-only installs still provide a local Server on port 5000; users can disable that user service when a system service already owns the Server. Desired capabilities are declared through `services.agent-up.capabilities` or `programs.agent-up.capabilities` and written to Agent-Up capability inventory. Runtime capability lookup reads `AGENTUP_CAPABILITY_INVENTORY_PATH` when set, then falls back to `/etc/agent-up/capabilities.json` and `~/.config/agent-up/capabilities.json`. The `agent-up-installer` launcher sets `AGENTUP_INSTALLER_NIXOS_LOOKUP_ONLY=1`; the dashboard can inspect component and capability state, but install/update/uninstall actions stay disabled because Nix owns system mutation.

Windows packaging uses WiX through `AgentUp.Packaging`. The packaging app generates `Product.wxs`, `Bundle.wxs`, the CLI shim, and the bootstrapper license file, stages required WiX extension DLLs on Windows, then invokes `wix build` for the staged `Product.msi` and bootstrapper executable.

The Windows bootstrapper executable chains the generated `Product.msi`; it must not launch `AgentUp.InstallerApp` midway through installation. The MSI installs `AgentUp.InstallerApp` as a standalone app under `Program Files\Agent-Up\installer`, installs a local `installer\payload` cache, and creates a separate Start Menu shortcut for opening the installer app after installation. The Burn success page may offer to launch the installed installer app after the MSI completes. The generated MSI is still copied to the named `agent-up-windows-<rid>.msi` sidecar artifact for direct native validation and troubleshooting.

Windows packaging consumes Windows install metadata, WiX generation, service, PATH, and shortcut contracts from `AgentUp.Installers`. Tests assert the generated WiX service, PATH, application shortcut, installer-app shortcut, MSI chain, bundled installer payload, MSI sidecar, and exact `wix` command shape with an isolated fake command runner.

Windows MSI metadata uses a Windows Installer product version derived from the package SemVer; CI fallback versions based on `0.0.0` are written as `0.0.1` in MSI metadata while retaining the original artifact version elsewhere.

macOS packaging uses `Product.pkg` through `AgentUp.Packaging`. The package installs only the dashboard `Agent-Up Installer.app`; Desktop, Server, CLI, launchd, symlink, validation, and uninstall behavior stay owned by the InstallerApp and its macOS adapter. The packaging app stages the InstallerApp bundle, its bundled offline `desktop`, `server`, and `cli` payload, installer-app package scripts, distribution XML, and `pkgbuild`/`productbuild` command shapes while consuming macOS install metadata, plist generation, and package scripts from `AgentUp.Installers`.

The macOS package postinstall script opens `/Applications/Agent-Up Installer.app` after installing or updating that app. The app resolves its bundled offline payload from `Contents/MacOS/payload` when `AGENTUP_INSTALLER_PAYLOAD_ROOT` is not set, checking both the app base directory and the real process executable directory so single-file extraction paths do not hide the installed bundle payload. The installer component removes any previous installer bundle before installing the new one so stale bundled files cannot survive package upgrades. Installer startup failures are appended to `~/Library/Logs/Agent-Up/installer-crash.log` before the app rethrows. Tests assert those generated files and commands on any platform; executing the final Apple packaging tools still requires Darwin.

When any native packaging tool is invoked from `AgentUp.Packaging`, tests should assert the exact command shape with an isolated fake command runner and smoke tests should verify the produced artifact on an appropriate runner.

`AgentUp.Packaging package <platform> <runtime-id> <version> [output-dir] --payload-root <path>` packages an existing payload root containing `installer`, `desktop`, `server`, and `cli` directories. Without `--payload-root`, the command keeps the local developer fallback of publishing those projects before packaging.

Package smoke scripts must use `AgentUp.PackageSmoke validate-package` for macOS, Windows, and Ubuntu artifact contract checks. macOS package smoke validates the InstallerApp-only `.pkg` and its bundled offline payload; macOS installed-service smoke is skipped until InstallerApp-driven service installation is enabled in CI after package installation. Windows package smoke validates both the Burn bootstrapper and the named MSI sidecar artifact, then runs the bootstrapper `/layout` command to prove the executable package can service layout requests without depending on Burn extracting embedded payloads to a specific directory. Installed-service smoke scripts must use `AgentUp.PackageSmoke validate-installed-service` for native installation, service readiness, installed CLI validation, diagnostics, and uninstall cleanup where the native package owns the completed install. Windows installed-service smoke validates native Apps & Features registration by DisplayName because MSI and Burn own the uninstall registry key names. Guided installer smoke launches the packaged `AgentUp.InstallerApp --smoke-installer-operations --payload-root <payload-root>` when `AGENTUP_INSTALLER_APP_COMMAND` is set, then runs `AgentUp.PackageSmoke validate-installer-flow <platform> <work-dir> [payload-root]` for the shared workflow contract. Passing a payload root lets the real platform adapter perform the installer work, while `AGENTUP_INSTALLER_FAKE=1` keeps tests non-privileged. CI uses fake mode for the guided installer flow because it validates the shipped executable and shared flow without taking machine-level install ownership. Shell smoke code should stay limited to argument forwarding and runner setup.

Windows installed-service smoke installs and uninstalls through the MSI sidecar with `msiexec`, starts the service after installation, and then validates readiness; the Windows `.exe` remains the user-facing bootstrapper path. Installed-service smoke also launches one .NET app and one Docker app through capability declarations, verifies they are registered with capability status, stops and restarts the .NET app independently, stops the Docker app independently, and then stops the workspace. The Docker capability sample uses `nginx:alpine` on Linux and macOS and a matching `mcr.microsoft.com/windows/servercore/iis` image on Windows runners. Windows CI pre-pulls that image and passes it through `AGENTUP_CAPABILITY_SMOKE_DOCKER_IMAGE` so image download time does not consume the installed CLI start timeout. Set `AGENTUP_CAPABILITY_SMOKE_SKIP_REAL=1` only for constrained runs that cannot launch real capability-backed apps.

The shared installer app is the target user-facing install, maintenance, capability-module, and update dashboard. Windows `.exe` artifacts install `AgentUp.InstallerApp` as a standalone app with a bundled release payload, and macOS `.pkg` postinstall launches `AgentUp.InstallerApp` with a bundled release payload. The app can install from its bundled offline payload or, when implemented, pull the latest online payload.

During migration, MSI sidecar and `.deb` artifacts may still perform direct native install work, but new user-facing behavior should move toward wrapping or launching `AgentUp.InstallerApp` with bundled and online payload selection.

Platform packaging owns native registration:

| Platform | Native package | Service | CLI target | Desktop target |
|---|---|---|---|---|
| Windows | `agent-up-windows-<rid>.exe` plus `agent-up-windows-<rid>.msi` | Windows Service | application `bin` directory on PATH | Start Menu and Apps & Features |
| macOS | InstallerApp-only `Product.pkg` | InstallerApp macOS adapter registers `launchd` | InstallerApp macOS adapter creates `/usr/local/bin/agent-up` | InstallerApp macOS adapter installs `/Applications/Agent-Up.app` |
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
