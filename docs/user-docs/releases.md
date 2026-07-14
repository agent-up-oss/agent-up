---
title: Releases
---

# Releases

Agent-Up release artifacts are built by CI after the platform test matrix passes.

Each platform job also smoke-tests the package it just built before upload. The smoke tests unpack the artifact on the target runner, check the expected Desktop, Server, CLI, installer, and service files, start the packaged Server from the unpacked payload, register an example `agent-up.json` workspace with the packaged CLI, and verify `agent-up status`.

The intended installed shape is:

- Desktop is the human UI.
- Server is installed and run as the local `agent-up-server` service.
- CLI and MCP clients connect to the same local Server URL, `http://localhost:5000`, unless `AGENTUP_SERVER_URL` points elsewhere.

## Platforms

CI builds artifacts for:

| Platform | Artifact |
|---|---|
| macOS | Desktop `.app` bundle plus launchd service plist and install/uninstall scripts for the bundled Server |
| Windows | Desktop and Server payloads plus PowerShell service install/uninstall scripts |
| Ubuntu | Desktop and Server payloads plus a systemd unit |
| NixOS | Desktop and Server payloads plus a systemd-oriented Nix module example |

The Server remains the runtime authority. Packaging the Server with Desktop only changes installation and startup; it does not move orchestration into Desktop.

The smoke test validates service wiring by checking the package's service registration files:

- macOS package install uses `launchctl bootstrap system` for `dev.agent-up.server`.
- Windows package install uses `New-Service` and `Start-Service` for `agent-up-server`.
- Ubuntu package install uses `systemctl enable --now agent-up-server.service`.
- NixOS package includes a `systemd.services.agent-up-server` module example.

CI also runs a privileged installed-service smoke test where the runner can host that service:

- macOS installs the LaunchDaemon, waits for the Server, registers an example workspace with the packaged CLI, then uninstalls it.
- Windows installs the Windows Service, waits for the Server, registers an example workspace with the packaged CLI, then uninstalls it.
- Ubuntu installs the systemd service, waits for the Server, registers an example workspace with the packaged CLI, then uninstalls it.
- NixOS validates the module in the package smoke test, but the installed-service smoke is skipped because the CI runner is Ubuntu with Nix, not a booted NixOS systemd host.

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

Required CI variable:

| Variable | Purpose |
|---|---|
| `AGENTUP_DOWNLOAD_BASE_URL` | Public base URL for published artifacts |

Optional CI variables:

| Variable | Default |
|---|---|
| `AGENTUP_CI_ARTIFACT_PREFIX` | `agent-up-ci` |
| `AGENTUP_RELEASE_PREFIX` | `agent-up` |
| `AGENTUP_ARTIFACT_DOWNLOAD_URL` | Empty; when set, the docs homepage shows a download button |

CI does not use GitHub Actions artifacts. Platform jobs upload package outputs directly to:

```text
{ci-prefix}/runs/{github-run-id}/{platform-runtime}/
```

The release job downloads those objects from MinIO, then publishes the final release artifacts.

CI publishes artifacts under both:

```text
{prefix}/releases/{version}/
{prefix}/latest/
```

Each prefix also receives a `manifest.json` listing the uploaded artifacts.

## Update Direction

The first update path should be explicit and service-aware:

- Publish immutable versioned artifacts and a mutable `latest/manifest.json`.
- Let the Desktop check the manifest and show an available update.
- Download the platform artifact from the configured bucket URL.
- Stop and replace the Server service through the platform installer path.
- Restart the Server, then restart or reload Desktop.

Automatic background updates should wait until signing, rollback, service migration, and user consent behavior are defined for each platform.
