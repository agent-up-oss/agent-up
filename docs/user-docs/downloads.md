---
title: Downloads
---

# Downloads

The latest Agent-Up release is available directly from GitHub Releases.

## Windows

Use the Windows installer:

<a className="button button--primary" href="https://github.com/agent-up-oss/agent-up/releases/latest/download/agent-up-windows-win-x64.exe">Download Windows installer</a>

Run the installer normally. It opens the Agent-Up Installer dashboard, where Desktop, Server, and CLI can each be installed, updated, repaired, or uninstalled from their own cards.

The installer registers Agent-Up under Windows Apps, adds a Start Menu entry, adds `agent-up` to PATH, installs the Server as the `agent-up-server` Windows Service, and removes all of those components on uninstall. CLI commands connect to the local Server at `http://localhost:5000` unless `AGENTUP_SERVER_URL` points somewhere else.

## macOS

Use the macOS package for your processor:

<a className="button button--primary" href="https://github.com/agent-up-oss/agent-up/releases/latest/download/agent-up-macos-osx-arm64.pkg">Download macOS Apple Silicon package</a>

<a className="button button--secondary" href="https://github.com/agent-up-oss/agent-up/releases/latest/download/agent-up-macos-osx-x64.pkg">Download macOS Intel package</a>

Open the `.pkg`. It installs and opens the Agent-Up Installer dashboard, where Desktop, Server, and CLI can each be managed from their own cards.

The installer copies `Agent-Up.app` into `/Applications`, installs the CLI under `/usr/local/agent-up/cli`, adds `agent-up` CLI symlinks under `/usr/local/bin`, and starts the bundled Server as the `dev.agent-up.server` launchd service.

## Ubuntu

Use the Debian package:

<a className="button button--primary" href="https://github.com/agent-up-oss/agent-up/releases/latest/download/agent-up-ubuntu-linux-x64.deb">Download Ubuntu package</a>

Install it with apt:

```bash
sudo apt install ./agent-up-ubuntu-linux-x64.deb
```

The package installs Agent-Up under `/opt/agent-up`, registers `agent-up-server.service`, and starts it through systemd. Start Desktop with:

```bash
/opt/agent-up/desktop/AgentUp.Desktop
```

The package also installs `agent-up` at `/usr/bin/agent-up` and registers the Desktop launcher as `agent-up.desktop` for the application menu.

## NixOS

Use the package-set tarball as a flake input:

```nix
inputs.agent-up.url = "tarball+https://github.com/agent-up-oss/agent-up/releases/latest/download/agent-up-nixos-pkgs.tar.gz";
```

Enable the Server through the NixOS module:

```nix
imports = [ inputs.agent-up.nixosModules.default ];
services.agent-up.enable = true;
```

This registers the system service as `agent-up-server.service` and listens on `http://127.0.0.1:5000` by default.

Declare desired capability versions through the NixOS module when needed:

```nix
services.agent-up.capabilities = {
  dotnet = [ "10.0.x" ];
  docker = [ "27.x" ];
};
```

Enable Desktop through the Home Manager module:

```nix
imports = [ inputs.agent-up.homeManagerModules.default ];
programs.agent-up.enable = true;
```

Home Manager also registers a `systemd --user` service named `agent-up-server.service` by default, so a Home Manager-only install has a local Server for Desktop and CLI. Set `programs.agent-up.server.enable = false` if the Server is already provided by the NixOS module.

Home Manager installs can declare capability versions with `programs.agent-up.capabilities` using the same shape.

The Nix package includes `agent-up-installer`. On NixOS this opens the same dashboard used on other platforms, but it is lookup-only: it shows installed Agent-Up commands and declared capability versions from `/etc/agent-up/capabilities.json` or `~/.config/agent-up/capabilities.json`, and install or version-management changes must still be made in NixOS or Home Manager configuration.

The tarball also exports `packages.x86_64-linux.agent-up`, `packages.x86_64-linux.default`, and `overlays.default`.

NixOS does not use the Windows, macOS, or Ubuntu binary installers.
