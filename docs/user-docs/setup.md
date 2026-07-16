---
title: Setup
---

# Setup

Agent-Up is currently a development preview. Packaged installers and platform packages are still being hardened while signing, update behavior, and service handling evolve.

Installed Desktop artifacts are expected to run the Server as a local background service. Desktop, CLI, and MCP clients connect to that service at `http://localhost:5000` unless `AGENTUP_SERVER_URL` points elsewhere.

If the service starts and stops repeatedly or consumes CPU while failing to become ready, check whether another application is already listening on port 5000. Packaged services restart with a 5 second backoff, but Agent-Up still needs an available local server port.

## Requirements

- .NET SDK 10.0 preview or compatible SDK for the current target framework.
- Git.
- Docker and Docker Compose when managed repositories declare Docker services.
- Node.js and npm for the documentation site.
- Nix on NixOS or Linux systems that need the provided native desktop dependency shell.

## Clone and Build

```bash
git clone https://github.com/themassiveone/agent-up.git
cd agent-up
dotnet restore agent-up.sln
dotnet build agent-up.sln
```

## Start the Server

When using an installed Desktop artifact, the Server should already be running as the local `agent-up-server` service.

For source development:

```bash
dotnet run --project AgentUp.Server
```

The development launch profile currently listens on:

```text
http://localhost:5000
```

## Start the Desktop

On systems with the required native desktop libraries available:

```bash
dotnet run --project AgentUp.Desktop
```

On NixOS:

```bash
./run-desktop.sh
```

The script runs the desktop inside `shell.nix`, which provides the native libraries needed by Avalonia, SkiaSharp, and WebKitGTK.

## Release Artifacts

See [Downloads](./downloads.md) for current platform download links and install commands.

## Configure a Repository

Add `agent-up.json` to the repository you want Agent-Up to manage:

```json
{
  "name": "My App",
  "applications": [
    {
      "name": "Frontend",
      "command": "npm run dev",
      "path": ".",
      "ports": [
        { "variable": "PORT", "defaultPort": 3000 }
      ]
    }
  ]
}
```

Applications should read ports from environment variables supplied by Agent-Up instead of hardcoding localhost ports.

## Register a Workspace

From the managed repository:

```bash
dotnet run --project /path/to/AgentUp.CLI -- start --server http://localhost:5000
```

This reads `agent-up.json`, captures the current Git branch and commit, and registers the workspace with the server.

## Contributor Workflow

Contributor testing and documentation builds are covered in the [Developer Guide](/developer-guide/workflows).

## Troubleshooting

- If the desktop fails to start on Linux, use `./run-desktop.sh` or install the native libraries listed in `shell.nix`.
- If the CLI cannot reach the server, pass `--server http://localhost:5000` explicitly or set `AGENTUP_SERVER_URL`.
- If an application does not bind correctly, confirm it reads the configured port environment variable.
- If Docker services fail, verify Docker and Docker Compose work outside Agent-Up first.
