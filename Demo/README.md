# Agent-Up Multi-Agent Demo

This directory contains four lightweight demo workspaces for recording multiple agents connected to the same Agent-Up Server.

Start each terminal inside one workspace root:

- `Demo/agent1`
- `Demo/agent2`
- `Demo/agent3`
- `Demo/agent4`

Each workspace contains:

- `agent-up.json` for Agent-Up workspace registration.
- `PROMPT.md` with a five-minute MCP browser/audit task for the terminal agent.
- Several dependency-free Node HTTP apps that mirror the marketing-site interactive demo.

The apps intentionally use only Node built-in modules. No `npm install` step is required.

The demo workspaces use non-overlapping default port ranges so all four can run on the same host:

- `agent1`: `5100`-`5103`
- `agent2`: `5200`-`5203`
- `agent3`: `5300`-`5303`
- `agent4`: `5400`-`5403`

Suggested recording flow:

1. Start the Agent-Up Server and Desktop.
2. Open four terminals, one in each `Demo/agentx` directory.
3. Give each terminal agent its local `PROMPT.md`.
4. Let the agents start their workspaces and use MCP browser/audit tools while OBS records the Desktop scene.

The fourth workspace is an altered version of the online-shop workspace focused on returns and fulfillment.
