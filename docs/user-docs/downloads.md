---
title: Downloads
---

# Downloads

The latest Agent-Up release is published to the public release bucket under stable download URLs. CI overwrites the `agent-up/latest/` files on every release after the platform tests and package smoke tests pass.

## Windows

Use the Windows installer:

<a className="button button--primary" href="https://s3.massivecreationlab.com/agentup-release/agent-up/latest/agent-up-windows-win-x64.exe">Download Windows installer</a>

Run the installer normally. It prompts for elevation when needed, then lets you install, upgrade, or uninstall Agent-Up.

The installer registers Agent-Up under Windows Apps, adds a Start Menu entry, adds `agent-up` to PATH, installs the Server as the `agent-up-server` Windows Service, and removes all of those components on uninstall. CLI commands connect to the local Server at `http://localhost:5000` unless `AGENTUP_SERVER_URL` points somewhere else.

## macOS

Use the macOS disk image for your processor:

<a className="button button--primary" href="https://s3.massivecreationlab.com/agentup-release/agent-up/latest/agent-up-macos-osx-arm64.dmg">Download macOS Apple Silicon disk image</a>

<a className="button button--secondary" href="https://s3.massivecreationlab.com/agentup-release/agent-up/latest/agent-up-macos-osx-x64.dmg">Download macOS Intel disk image</a>

Open the `.dmg`, then open `Agent-Up Installer.app`. The installer prompts for administrator access and lets you install, upgrade, or uninstall Agent-Up.

The installer copies `Agent-Up.app` into `/Applications`, adds `agent-up` CLI symlinks under `/usr/local/bin`, and starts the bundled Server as the `dev.agent-up.server` launchd service. Uninstall removes those registrations and installed assets.

## Ubuntu

Use the Debian package:

<a className="button button--primary" href="https://s3.massivecreationlab.com/agentup-release/agent-up/latest/agent-up-ubuntu-linux-x64.deb">Download Ubuntu package</a>

Install it with apt:

```bash
sudo apt install ./agent-up-ubuntu-linux-x64.deb
```

The package installs Agent-Up under `/opt/agent-up`, registers `agent-up-server.service`, and starts it through systemd. Start Desktop with:

```bash
/opt/agent-up/desktop/AgentUp.Desktop
```

## NixOS

Use the package-set tarball as a flake input:

```nix
inputs.agent-up.url = "tarball+https://s3.massivecreationlab.com/agentup-release/agent-up/latest/agent-up-nixos-pkgs.tar.gz";
```

Enable the Server through the NixOS module:

```nix
imports = [ inputs.agent-up.nixosModules.default ];
services.agent-up.enable = true;
```

Enable Desktop through the Home Manager module:

```nix
imports = [ inputs.agent-up.homeManagerModules.default ];
programs.agent-up.enable = true;
```

The tarball also exports `packages.x86_64-linux.agent-up`, `packages.x86_64-linux.default`, and `overlays.default`.

NixOS does not use the Windows, macOS, or Ubuntu binary installers.
