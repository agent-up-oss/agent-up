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

## Health and metrics

Each demo workspace configures `healthCheck` and `metrics` paths on its primary API-style app:

- `agent1` Backend (`5102`): `/health`, `/metrics`
- `agent2` Worker (`5202`): `/health`, `/metrics`
- `agent3` Payments (`5302`): `/health`, `/metrics`
- `agent4` Fulfillment (`5402`): `/health`, `/metrics`

After starting a workspace in Agent-Up Desktop, open the **Metrics** tab on that application's HTTP port. The Server pulls JSON metrics every 30 seconds and records them in the audit trail (`scope=application`). Browser navigation to `/health` or `/metrics` still renders the HTML demo pages; Agent-Up and `curl` receive JSON.

Metrics are computed from **live HTTP traffic** to each demo app: request counts in the last 60 seconds, average response latency, error totals, process uptime, and heap usage. Browsing the app in Desktop or waiting for Agent-Up health/metrics pulls will change the numbers.

If you change `agent-up.json` after a workspace was first registered, stop the workspace and start it again so the Server reloads port `healthCheck` and `metrics` paths from disk.

Example metrics response (values change with traffic):

```json
{
  "metrics": {
    "latency_ms": 4,
    "requests_per_minute": 16,
    "errors_total": 0,
    "uptime_percent": 100,
    "requests_total": 42,
    "uptime_seconds": 180,
    "heap_mb": 12
  }
}
```
