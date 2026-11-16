---
title: Setup
---

# Setup

<DocWhat>
Setup is how you install Agent-Up and connect a client to the Server. Installed Desktop artifacts run the Server as a local background service.
</DocWhat>

<DocCallout kind="warning">
<DocFact label="Packaged">{'http://localhost:5000'}</DocFact>
<DocFact label="Repository">{'http://localhost:5001'}</DocFact>
</DocCallout>

<DocCallout kind="warning" label="Development Preview">
Packaged installers and platform packages are still being hardened while signing, update behavior, and service handling evolve.
</DocCallout>

If the service starts and stops repeatedly or consumes CPU while failing to become ready, check whether another application is already listening on port 5000. Packaged services restart with a 5 second backoff, but Agent-Up still needs an available local server port.

## Requirements

- .NET SDK 10.0 or a compatible SDK for the current target framework.
- Git.
- Docker and Docker Compose when managed repositories declare Docker services.
- Node.js and npm for the documentation site.
- Nix on the host that runs the Server. Capability packages deliver toolchains through Nix. On Linux or macOS:

```bash
sh <(curl -L https://nixos.org/nix/install) --daemon
```

See [the Nix download page](https://nixos.org/download). On Windows, run the Server under WSL2 and install Nix there. Nix is also used on NixOS or Linux when you need the native desktop dependency shell.

## Clone and Build

```bash
git clone https://github.com/themassiveone/agent-up.git
cd agent-up
dotnet restore agent-up.sln
dotnet build agent-up.sln
```

Packaged installers and GitHub Release assets are published from `https://github.com/agent-up-oss/agent-up`. Clone and contribute against `themassiveone/agent-up`.

## Start the Server

When using an installed Desktop artifact, the Server should already be running as the local `agent-up-server` service.

REST authentication is off until you set `AGENTUP_ADMIN_PASSWORD` or
`AGENTUP_AUTH_MODE=externalBearer`. Copy `.env.example` to `.env` in the repository
root and uncomment those values when you want a protected Server. Server, Desktop, and CLI load that file on startup
when it exists; shell environment variables still take precedence.

```bash
cp .env.example .env
# edit .env, then:
dotnet run --project AgentUp.Server
```

You can still export the password directly when you prefer:

```bash
export AGENTUP_ADMIN_PASSWORD='choose-a-long-password'
dotnet run --project AgentUp.Server
```

Desktop and Mobile prompt for this password only when the Server requires it.
With no authentication environment variables, they connect without a login form.

Desktop and Mobile can save more than one Server. Switching to another saved
Server opens that Server's workspaces as a new space on the client; the
previous Server's local window or app state is not kept. Saved sign-in tokens
stay on the device so the administrator password is not typed again until that
Server rejects them. Mobile then returns to the connect screen and asks for
the password again.

To force authentication off even after a password or bearer mode is configured, set
`AGENTUP_AUTH_DISABLED=true`. Desktop and Mobile skip their login UI in
that mode.

To expose REST to the LAN, set `AGENTUP_ADMIN_PASSWORD` or
`AGENTUP_AUTH_MODE=externalBearer`, bind the Server to your network interface, and
protect it with HTTPS or a TLS-terminating reverse proxy. TLS encrypts the
connection; it does not stop a LAN client from calling REST. Leave authentication
off only if anonymous LAN access is intentional. Plain HTTP exposes the
administrator password during login and bearer tokens on the wire. MCP stays
loopback-only until `AGENTUP_MCP_REMOTE_ENABLED=true` with `externalBearer`; a
request from another address without a valid token still looks like nothing is
there.

Remote Desktop and Mobile clients must use HTTPS when the Server URL is not
loopback. Loopback HTTP remains supported for local development on
`http://localhost` and `http://127.0.0.1`.

For source development:

```bash
dotnet run --project AgentUp.Server
```

The development launch profile currently listens on:

```text
http://localhost:5001
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

The script runs the desktop inside `shell.nix`, which provides the native libraries needed on Linux.

On every launch, Desktop and Mobile list a built-in **Demo** server on the connect screen. Desktop shows that picker before it talks to a local Server, so Demo can be opened without a running `agent-up-server`. Connecting to Demo uses only in-app sample state — a team of three named agents (Bob building a feature, Lucy waiting on your approval to post a review, Hector stopped on a repository host that will not answer), each with their own worktree, branch, and account on the host, plus interactive storefront and Orders API pages, Git changes, and first-party capability modules. Storefront and Orders API each ship several validation flows that reset the catalog, drive a user action, and assert the page after that change. It is not a running Agent-Up Server. Disconnect and connect to a real Server without restarting the client.

See [Workspaces](/docs/workspaces) for connect, start, and the Mobile workspace list.

## Release Artifacts

See [Downloads](/docs/start/downloads) for current platform download links and install commands.

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
dotnet run --project /path/to/AgentUp.CLI -- start --server http://localhost:5001
```

This reads `agent-up.json`, captures the current Git branch and commit, and registers the workspace with the server.

## Contributor Workflow

Contributor testing and documentation builds are covered in the [Developer Guide](/developer-guide/workspaces/workflows).

## Troubleshooting

- If the desktop fails to start on Linux, use `./run-desktop.sh` or install the native libraries listed in `shell.nix`.
- If the CLI cannot reach the server, pass `--server` explicitly (`http://localhost:5001` for the repository launch profile, `http://localhost:5000` for a packaged service) or set `AGENTUP_SERVER_URL`.
- If an application does not bind correctly, confirm it reads the configured port environment variable.
- If Docker services fail, verify Docker and Docker Compose work outside Agent-Up first.
