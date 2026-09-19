---
title: Configuration
---

<DocEyebrow slice="Configuration" status="available" />

# Configuration

<DocWhat>
`agent-up.json` at the repository root is the declarative contract the Server reads when a workspace starts.

The file declares applications, local services, capability requirements, ports, and launch-time environment. Applications do not reference Agent-Up packages, SDKs, or APIs.
</DocWhat>

<DocSpine>
<DocBeat>Place `agent-up.json` at the repository root</DocBeat>
<DocBeat>Prefer `dotnet` and `docker` capability sections</DocBeat>
<DocBeat>Start the workspace so the Server owns the result</DocBeat>
</DocSpine>

<DocContract label="File">agent-up.json</DocContract>

<DocCallout>
Only `name` is required. All application and service arrays are optional.
</DocCallout>

## Top-level shape

```json
{
  "name": "Inventory",
  "display": {},
  "applications": [],
  "desktopApplications": [],
  "services": [],
  "dotnet": [],
  "docker": [],
  "prompts": {},
  "commits": {},
  "verification": {},
  "coverage": {}
}
```

Use optional `display` values to override the Desktop workspace list title and subtitle without changing repository, worktree, or Git identity handling. `display.name` replaces the visible workspace title, and `display.branch` replaces the visible branch subtitle.

Use optional `prompts` values, such as `prompts.commitPolicy`, to give AI agents repository-specific guidance without changing application runtime configuration.

## Preferred sections

Use capability-aware sections when Agent-Up should understand the ecosystem boundary:

- `dotnet` for .NET applications launched through the .NET capability.
- `docker` for Docker containers launched through the Docker capability.

Use compatibility sections when Agent-Up should preserve a legacy executable-plus-arguments command or Docker service shape:

- `applications` for local executable-plus-arguments applications.
- `desktopApplications` for Linux graphical applications shown as streamed Desktop and Mobile tabs.
- `services` for legacy Docker services.
- `commits` to opt into the Server-owned proposal queue (`enabled`) or attach legacy local-queue test commands.
- `verification` and `coverage` for path-rule checks and coverage floors. See the [reference](/docs/configuration/reference#verification-object).

The complete field contract is in the [reference](/docs/configuration/reference).

## Capability-aware example

```json
{
  "name": "Inventory",
  "dotnet": [
    {
      "name": "API",
      "sdk": "10.0.x",
      "run": {
        "project": "src/Api/Api.csproj",
        "arguments": ["--no-launch-profile"]
      },
      "environmentFiles": [".env"],
      "environment": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "ports": [{ "variable": "API_PORT", "defaultPort": 5000 }]
    }
  ],
  "docker": [
    {
      "name": "Database",
      "image": "postgres:17",
      "environmentFiles": [".env.database"],
      "ports": [{ "variable": "DB_PORT", "defaultPort": 5432 }]
    }
  ]
}
```

Legacy executable commands remain supported when Agent-Up should only launch the process and inject ports. Agent-Up launches local application commands directly with an argument list; shell expressions such as pipes, redirects, variable expansion, command chaining, and subshells are rejected. Put that behavior in a checked-in script and call the script through an allowed runtime command when needed.

## Environment and secrets

Every launchable section supports:

- `environment` for inline non-secret values.
- `environmentFiles` for `.env`-style files such as `.env`, `.env.local`, or `.env.database`.

See [environment and secrets](/docs/configuration/environment) for precedence, parsing, storage, and Docker behavior.

## Examples

See [examples](/docs/configuration/examples) for complete capability-aware and legacy configuration files.

<DocNext href="/docs/configuration/reference" title="Reference">
The complete field contract.
</DocNext>
