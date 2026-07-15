---
title: Downloads
---

# Downloads

The latest Agent-Up release is published to the public release bucket under stable download URLs. CI overwrites the `agent-up/latest/` files on every release after the platform tests and package smoke tests pass.

## Windows

Use the Windows installer:

<a className="button button--primary" href="https://s3.massivecreationlab.com/agentup-release/agent-up/latest/agent-up-windows-win-x64.exe">Download Windows installer</a>

Run the installer from an elevated PowerShell session. It installs Desktop, CLI, and the Server payload, then starts the Server as the `agent-up-server` Windows Service.

After installation, start Agent-Up from the installed Desktop application. CLI commands connect to the local Server at `http://localhost:5000` unless `AGENTUP_SERVER_URL` points somewhere else.

## macOS

Use the macOS disk image for your processor:

<a className="button button--primary" href="https://s3.massivecreationlab.com/agentup-release/agent-up/latest/agent-up-macos-osx-arm64.dmg">Download macOS Apple Silicon disk image</a>

<a className="button button--secondary" href="https://s3.massivecreationlab.com/agentup-release/agent-up/latest/agent-up-macos-osx-x64.dmg">Download macOS Intel disk image</a>

Open the `.dmg`, run `install.sh`, then start `Agent-Up.app` from `/Applications`.

The installer copies `Agent-Up.app` into `/Applications` and starts the bundled Server as the `dev.agent-up.server` launchd service.

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

Use the package-set tarball as a flake input, then install `agent-up` from that input.

```nix
{
  inputs.agent-up.url = "tarball+https://s3.massivecreationlab.com/agentup-release/agent-up/latest/agent-up-nixos-pkgs.tar.gz";

  outputs = { nixpkgs, agent-up, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
      agentUp = agent-up.packages.${system}.agent-up;
    in
    {
      nixosConfigurations.example = nixpkgs.lib.nixosSystem {
        inherit system;
        modules = [
          {
            environment.systemPackages = [ agentUp ];

            systemd.services.agent-up-server = {
              description = "Agent-Up Server";
              wantedBy = [ "multi-user.target" ];
              after = [ "network.target" ];
              serviceConfig = {
                ExecStart = "${agentUp}/bin/agent-up-server --urls http://127.0.0.1:5000";
                Restart = "on-failure";
                RestartSec = 5;
                StateDirectory = "agent-up";
                Environment = [
                  "ASPNETCORE_URLS=http://127.0.0.1:5000"
                  "Storage__DataDirectory=/var/lib/agent-up"
                ];
              };
            };
          }
        ];
      };
    };
}
```

For a temporary shell, use `--no-write-lock-file` unless you are inside a flake workspace where Nix can update `flake.lock`:

```bash
nix shell --no-write-lock-file "tarball+https://s3.massivecreationlab.com/agentup-release/agent-up/latest/agent-up-nixos-pkgs.tar.gz#agent-up"
```

NixOS does not use the Windows, macOS, or Ubuntu binary installers.
