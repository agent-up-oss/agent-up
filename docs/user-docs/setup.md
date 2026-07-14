---
title: Setup
---

# Setup

Agent-Up is currently a source-only development preview. There are no stable desktop downloads or general-purpose installers.

Agent-Up may work on additional platforms, but the current development setup has only been verified on NixOS.

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

## Run Tests

```bash
dotnet test agent-up.sln
```

On NixOS or headless Linux setups:

```bash
nix-shell shell.nix --run "dotnet test agent-up.sln"
```

## Build Documentation

```bash
npm --prefix docs install
npm --prefix docs run build
```

## Troubleshooting

- If the desktop fails to start on Linux, use `./run-desktop.sh` or install the native libraries listed in `shell.nix`.
- If the CLI cannot reach the server, pass `--server http://localhost:5000` explicitly or set `AGENTUP_SERVER_URL`.
- If an application does not bind correctly, confirm it reads the configured port environment variable.
- If Docker services fail, verify Docker and Docker Compose work outside Agent-Up first.
