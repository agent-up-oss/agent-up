---
title: Releases
---

# Releases

Agent-Up release artifacts are built by CI after the Ubuntu .NET build and test job passes.

The Ubuntu build job builds the solution, runs the broad .NET test suite, publishes the reusable Packaging and PackageSmoke console artifacts, and publishes single-file self-contained Desktop, Server, and CLI payloads for each release runtime. Native platform jobs download only their own runtime's prebuilt payloads from short-lived GitHub Actions artifacts, delete those consumed CI-transfer artifacts, build only the native package format, and smoke-test the package before upload. Package smoke tests consume the artifact on the target runner and check the expected package contract. Where the package exposes payload files directly, smoke tests also start the packaged Server from the package payload, register an example `agent-up.json` workspace with the packaged CLI, and verify `agent-up status`. Windows WiX/Burn artifacts are validated through layout/package checks first, then through the installed-service smoke path.

Shared installer planning, validation, and platform install contracts live in `AgentUp.Installers` and are tested by `AgentUp.Installers.Tests`. The shared guided installer UX lives in `AgentUp.InstallerApp` and is tested by `AgentUp.InstallerApp.Tests` plus native-display flow tests in `AgentUp.Tests`. Release artifact staging and native tool orchestration lives in `AgentUp.Packaging` and is tested by `AgentUp.Packaging.Tests`; packaging code consumes the shared installer contracts instead of redefining platform behavior. Package and installed-service smoke validation lives in `AgentUp.PackageSmoke` and is tested by `AgentUp.PackageSmoke.Tests`; CI smoke scripts call this shared console validator for artifact, installer-flow, install, service, CLI, diagnostics, and uninstall checks. Native package smoke tests cover the platform-specific contract that cannot be proven by unit tests, such as service registration, fresh-shell CLI availability, desktop launcher metadata, and uninstall behavior.

Developers should use the Nix packaging wrappers when building release artifacts from NixOS or when they want a pinned packaging environment:

```bash
./scripts/package-ubuntu.sh linux-x64 0.0.0-local artifacts
./scripts/package-windows.sh win-x64 0.0.0-local artifacts
./scripts/package-macos.sh osx-arm64 0.0.0-local artifacts
```

The macOS wrapper must run on Darwin because Apple package/sign/notarization tools are not available on Linux.

Windows packaging uses WiX-generated `Product.msi` and `Setup.exe` artifacts orchestrated by `AgentUp.Packaging`.

macOS packaging is moving from the legacy `.dmg` script installer toward a `Product.pkg` artifact orchestrated by `AgentUp.Packaging`.

The intended installer direction is a shared Avalonia guided installer distributed through platform-native wrappers. Each release wrapper should include a bundled payload for offline installation, while the installer can offer an online newer payload when release metadata is reachable.

Ubuntu, macOS, and Windows have real guided-installer adapters. The installer app uses the real adapter by default when `AGENTUP_INSTALLER_PAYLOAD_ROOT` points at a staged payload; non-privileged tests opt into the fake adapter with `AGENTUP_INSTALLER_FAKE=1`. CI still validates the native package and installed-service paths.

The intended installed shape is:

- Desktop is the human UI.
- Server is installed and run as the local `agent-up-server` service.
- CLI and MCP clients connect to the same local Server URL, `http://localhost:5000`, unless `AGENTUP_SERVER_URL` points elsewhere.

## Platforms

CI builds artifacts for:

| Platform | Artifact |
|---|---|
| macOS Apple Silicon | `.pkg` containing Desktop, CLI, Server launchd service, and package scripts |
| macOS Intel | `.pkg` containing Desktop, CLI, Server launchd service, and package scripts |
| Windows | self-elevating `.exe` GUI installer containing Desktop, CLI, Server, Windows Service scripts, Windows Apps registration, Start Menu entry, and PATH setup |
| Ubuntu | `.deb` package installing Desktop, CLI, Server, and `agent-up-server.service` |
| NixOS | package-set tarball consumed as a flake input exposing a package, overlay, NixOS module, and Home Manager module |

The Server remains the runtime authority. Packaging the Server with Desktop only changes installation and startup; it does not move orchestration into Desktop.

The smoke test validates service wiring by checking the package's service registration files:

- macOS package install uses `launchctl bootstrap system` for `dev.agent-up.server` and registers `agent-up` CLI symlinks under `/usr/local/bin`.
- Windows package install uses WiX `ServiceInstall` and `ServiceControl` for `agent-up-server`, registers Agent-Up under Windows Apps, creates a Start Menu entry, and adds `agent-up` to PATH.
- Ubuntu package install uses `systemctl enable --now agent-up-server.service`.
- Ubuntu installs the global CLI entry at `/usr/bin/agent-up` and registers the Desktop launcher through `/usr/share/applications/agent-up.desktop`.
- NixOS package smoke validates that the package-set tarball is a valid locked flake, exposes `packages.x86_64-linux.agent-up`, `overlays.default`, `nixosModules.default`, and `homeManagerModules.default`, patches the bundled Linux binaries through Nix, wraps the required native runtime libraries, and includes `logo.png` at `/opt/agent-up/logo.png` for the Home Manager desktop entry.

Auto-restarting service definitions must use a 5 second restart throttle so port conflicts or other startup failures do not create a tight restart loop.

CI also runs a privileged installed-service smoke test where the runner can host that service:

- macOS installs the LaunchDaemon, waits for the Server, registers an example workspace with the packaged CLI, then uninstalls it.
- Windows installs the Windows Service, waits for the Server, registers an example workspace with the packaged CLI, then uninstalls it.
- Ubuntu installs the systemd service, waits for the Server, registers an example workspace with the packaged CLI, then uninstalls it.
- NixOS validates the package-set tarball in the package smoke test, but the installed-service smoke is skipped because the CI runner is Ubuntu with Nix, not a booted NixOS systemd host.

## MinIO/S3 Release Upload

Release upload uses the MinIO `mc` client against an S3-compatible endpoint.

Required CI secrets:

| Secret | Purpose |
|---|---|
| `AGENTUP_RELEASE_BUCKET` | Public-read release bucket name |
| `AGENTUP_RELEASE_S3_ENDPOINT` | MinIO/S3-compatible endpoint URL for the release bucket |
| `AGENTUP_RELEASE_S3_ACCESS_KEY` | Private write access key for the release bucket |
| `AGENTUP_RELEASE_S3_SECRET_KEY` | Private write secret key for the release bucket |

CI transfer uses GitHub Actions artifacts rather than the release S3 endpoint. The Ubuntu build job uploads one `dotnet-ci-{platform}-{runtime}` artifact per platform job with one-day retention. Each platform job deletes its consumed .NET artifact immediately after download. Platform jobs upload one `package-{platform}-{runtime}` artifact with one-day retention. The release job downloads those package artifacts, deletes them immediately, then publishes the final release artifacts to the release bucket.

CI publishes artifacts under both:

```text
agent-up/releases/{version}/
agent-up/latest/
```

The `agent-up/latest/` prefix is overwritten on every release, so the stable download URLs in the Downloads page always point at the latest release.

The release upload also writes `manifest.json` and `checksums.sha256` beside the versioned and latest artifacts so installers and update checks can consume a stable release contract.

## Update Direction

The first update path should be explicit and service-aware:

- Publish immutable versioned artifacts and mutable `latest/` files for each platform.
- Let the Desktop check the versioned release metadata and show an available update.
- Download the platform installer from the public release bucket.
- Stop and replace the Server service through the platform installer path.
- Restart the Server, then restart or reload Desktop.

Automatic background updates should wait until signing, rollback, service migration, and user consent behavior are defined for each platform.
