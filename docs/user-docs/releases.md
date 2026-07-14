---
title: Releases
---

# Releases

Agent-Up release artifacts are built by CI after the platform test matrix passes.

Each platform job also smoke-tests the package it just built before upload. The smoke tests consume the artifact on the target runner, check the expected Desktop, Server, CLI, installer, and service files, start the packaged Server from the package payload, register an example `agent-up.json` workspace with the packaged CLI, and verify `agent-up status`.

The intended installed shape is:

- Desktop is the human UI.
- Server is installed and run as the local `agent-up-server` service.
- CLI and MCP clients connect to the same local Server URL, `http://localhost:5000`, unless `AGENTUP_SERVER_URL` points elsewhere.

## Platforms

CI builds artifacts for:

| Platform | Artifact |
|---|---|
| macOS Apple Silicon | `.dmg` containing `Agent-Up.app`, launchd service plist, and install/uninstall scripts |
| macOS Intel | `.dmg` containing `Agent-Up.app`, launchd service plist, and install/uninstall scripts |
| Windows | `.exe` installer containing Desktop, CLI, Server, and Windows Service scripts |
| Ubuntu | `.deb` package installing Desktop, CLI, Server, and `agent-up-server.service` |
| NixOS | package-set tarball consumed as a flake input exposing `agentsPkgs.agent-up` |

The Server remains the runtime authority. Packaging the Server with Desktop only changes installation and startup; it does not move orchestration into Desktop.

The smoke test validates service wiring by checking the package's service registration files:

- macOS package install uses `launchctl bootstrap system` for `dev.agent-up.server`.
- Windows package install uses `New-Service` and `Start-Service` for `agent-up-server`.
- Ubuntu package install uses `systemctl enable --now agent-up-server.service`.
- NixOS package smoke validates that the package-set tarball exposes `agentsPkgs.agent-up` through its flake output.

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
| `AGENTUP_CI_ARTIFACT_BUCKET` | Private bucket used to pass platform packages from matrix jobs to the release job |
| `AGENTUP_CI_S3_ENDPOINT` | MinIO/S3-compatible endpoint URL for the private CI bucket |
| `AGENTUP_CI_S3_ACCESS_KEY` | Read/write access key for the private CI bucket |
| `AGENTUP_CI_S3_SECRET_KEY` | Read/write secret key for the private CI bucket |
| `AGENTUP_RELEASE_BUCKET` | Public-read release bucket name |
| `AGENTUP_RELEASE_S3_ENDPOINT` | MinIO/S3-compatible endpoint URL for the release bucket |
| `AGENTUP_RELEASE_S3_ACCESS_KEY` | Private write access key for the release bucket |
| `AGENTUP_RELEASE_S3_SECRET_KEY` | Private write secret key for the release bucket |

CI does not use GitHub Actions artifacts. Platform jobs upload package outputs directly to:

```text
agent-up-ci/runs/{github-run-id}/{platform-runtime}/
```

The release job downloads those objects from MinIO, then publishes the final release artifacts.

CI publishes artifacts under both:

```text
agent-up/releases/{version}/
agent-up/latest/
```

The `agent-up/latest/` prefix is overwritten on every release, so the stable download URLs in the Downloads page always point at the latest release.

## Update Direction

The first update path should be explicit and service-aware:

- Publish immutable versioned artifacts and mutable `latest/` files for each platform.
- Let the Desktop check the versioned release metadata and show an available update.
- Download the platform installer from the public release bucket.
- Stop and replace the Server service through the platform installer path.
- Restart the Server, then restart or reload Desktop.

Automatic background updates should wait until signing, rollback, service migration, and user consent behavior are defined for each platform.
