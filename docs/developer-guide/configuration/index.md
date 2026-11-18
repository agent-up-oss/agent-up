---
title: Configuration
---

<DocEyebrow slice="Configuration" status="available" />

# Configuration

<DocWhat>
`agent-up.json` is the repository contract the Server reads at workspace start. It declares applications, services, capability requirements, ports, verification, and optional `commits.enabled`.

Named `agent-up.json` sections match enabled runtime-kind capability ids. First-party `dotnet[]` and `docker[]` bind through that same generic path. Every capability is compiled into the Server; enablement is Server-owned in `capabilities/enabled.json`.
</DocWhat>

<DocMeta
  owner="Server configuration parsing plus AgentUp.Capabilities.*"
  tests="Server configuration suites and capability test projects"
  mcp="get_agent_up_json_format on /mcp/orchestration"
/>

<DocSpine>
<DocBeat>Read `get_agent_up_json_format` when the schema is unknown</DocBeat>
<DocBeat>Prefer capability sections over legacy executable strings</DocBeat>
<DocBeat>Enable the matching capability on the Server</DocBeat>
</DocSpine>

<DocContract label="Tool">get_agent_up_json_format</DocContract>

<DocFacts label="Orchestration resources">
<DocFact label="schema">agent-up://agent-up-json</DocFact>
<DocFact label="context">agent-up://context</DocFact>
</DocFacts>

## Built-in capabilities

Every capability is a project referenced by `AgentUp.Server` and listed in `FirstPartyCapabilityCatalog`: `dotnet`, `docker`, `codex`, `cursor`, `claude`, `opencode`, `github`, and `forgejo`. There is no registry, no package, and no DLL loading; a capability is always installed and enabling it only decides whether the Server offers its feature.

Each capability implements `ICapability` from `AgentUp.Sdk.Common` through its kind contract (`IRuntimeCapability`, `IAgentCapability`, `IForgeCapability`):

- `Software` names what it runs. An entry `DeliveredByNix` needs Nix; any other entry must be found on the Server's search path, which includes the `.agent-up-dev` bin directories.
- `Settings` names what a user configures once it is enabled. A `secret` value is stored but never returned.
- A runtime's `Provides` lists the commands its Nix packages put on the path.

`CapabilitySoftwareProbe` checks `Software`. While anything is missing the capability is `unavailable`, and `POST /api/capabilities/enable` answers `409`. An enabled capability whose software later disappears stays enabled but reports `unavailable` and cannot run. The states are `unavailable`, `disabled`, `needsSetup`, and `ready`.

`PUT /api/capabilities/{id}/settings` changes the keys it is given and keeps the rest; an empty string clears one. A `url` setting must be an absolute http or https URL. Values live in `capabilities/settings.json` under the Server data directory, readable by the Server user only.

`AGENTUP_CAPABILITY_ENABLED_PATH` seeds `capabilities/enabled.json`. Helm, NixOS, and `nix-shell shell.nix` write that seed as a list of ids. The repository launch profile points it at `.agent-up-dev/enabled.json`. Runtime enable and disable through REST and MCP update the Server copy without a restart.

`dotnet[]` and `docker[]` are named runtime sections parsed only when that runtime is enabled. Any other enabled runtime-kind capability id can appear as a root array and is bound with that capability's extra-attribute schema: unknown extra keys fail, and missing required extra keys fail. `agent-up start` forwards those named arrays as `runtimeSections` without binding them. Technology version (`sdk: "10.0.x"`) is an input to `IRuntimeCapability.Deliver`. The repository example API and Postgres service use those sections; CI starts the repository-root `agent-up.json` from Desktop and replays its recorded Example Web validation flow.

The version a capability runs is decided in code. `CapabilityNixShellProvider` writes `FirstPartyNixPin.DefaultNix(packages)` under `capabilities/nix/` in the data directory, pinned to the first-party nixpkgs commit, and every wrapped launch enters that shell. A launch that combines an agent with enabled runtimes gets one shell holding all of their packages. A `dotnet-sdk*` shell sets `DOTNET_ROOT` to that Nix SDK and unsets testhost `MSBuildSDKsPath` so `dotnet run` writes `runtimeconfig.json` and the apphost finds `libhostpolicy.so` under `shared/Microsoft.NETCore.App`. It also puts `icu` and `openssl` on `LD_LIBRARY_PATH`, because the runtime opens those two with `dlopen` by soname and aborts at startup without them. Shells are `mkShellNoCC`, so enabling a capability does not deliver a C toolchain. Legacy `applications` wrap through Nix only when an enabled runtime lists that executable in `Provides`.

Forge capabilities implement `IForgeCapability`, need no software on the host, and have no Nix closure and no ACP session. Host URLs live on the capability. Server Forge copy never names a particular host. There is no non-Nix install path. Native Windows Server hosts must use WSL2/Linux.

<DocNext href="/docs/configuration/reference" title="User reference">
The JSON field contract.
</DocNext>
