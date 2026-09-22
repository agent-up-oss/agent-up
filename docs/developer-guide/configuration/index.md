---
title: Configuration
---

<DocEyebrow slice="Configuration" status="available" />

# Configuration

<DocWhat>
`agent-up.json` is the repository contract the Server reads at workspace start. It declares applications, services, capability requirements, ports, verification, and optional `commits.enabled`.

Named `agent-up.json` sections match enabled runtime-kind module ids. First-party `dotnet[]` and `docker[]` bind through that same generic path. Enablement is Server-owned (`capabilities/enabled.json` stores package id plus package version); Desktop and Mobile never call the remote registry.
</DocWhat>

<DocMeta
  owner="Server configuration parsing plus AgentUp.Capabilities.*"
  tests="Server configuration suites and capability test projects"
  mcp="get_agent_up_json_format on /mcp/orchestration"
/>

<DocSpine>
<DocBeat>Read `get_agent_up_json_format` when the schema is unknown</DocBeat>
<DocBeat>Prefer capability sections over legacy executable strings</DocBeat>
<DocBeat>Enable the matching registry package on the Server</DocBeat>
</DocSpine>

<DocContract label="Tool">get_agent_up_json_format</DocContract>

<DocFacts label="Orchestration resources">
<DocFact label="schema">agent-up://agent-up-json</DocFact>
<DocFact label="context">agent-up://context</DocFact>
</DocFacts>

## Capability registry

The Server reads enabled packages from its local registry directory (`AGENTUP_CAPABILITY_REGISTRY_PATH`) and the enabled set (`AGENTUP_CAPABILITY_ENABLED_PATH` seeds `capabilities/enabled.json` under the Server data directory). The repository launch profile in `AgentUp.Server/Properties/launchSettings.json` sets those paths to `.agent-up-dev/capability-registry` and `.agent-up-dev/enabled.json` with the working directory at the repository root. When that path is unset, a development Server also uses `.agent-up-dev/capability-registry` if `scripts/pack-first-party-capabilities.sh` has written `index.json` there. Helm and NixOS write that enabled-set seed. Runtime enable/disable through REST and MCP updates the Server copy without rebuilding the image.

`dotnet[]` and `docker[]` are named runtime sections parsed only when that runtime module is enabled. Any other enabled runtime-kind module id can appear as a root array and is bound with that module's extra-attribute schema: unknown extra keys fail, and missing required extra keys fail. `agent-up start` forwards those named arrays as `runtimeSections` without binding them. Package version (`dotnet@1.0.0` in `enabled.json`) is the module contract. Technology version (`sdk: "10.0.x"`) is an input to `IRuntimeCapability.Deliver`. The repository example API and Postgres service use those sections; CI starts the repository-root `agent-up.json` from Desktop and replays its recorded Example Web validation flow. Packed `default.nix` for a `dotnet-sdk*` module sets `DOTNET_ROOT` to that Nix SDK and unsets testhost `MSBuildSDKsPath` so `dotnet run` writes `runtimeconfig.json` and the apphost finds `libhostpolicy.so` under `shared/Microsoft.NETCore.App`. It also puts `icu` and `openssl` on `LD_LIBRARY_PATH`, because the runtime opens those two with `dlopen` by soname and aborts at startup without them. Packed shells are `mkShellNoCC`, so enabling a module does not deliver a C toolchain. Legacy `applications` wrap through Nix only when an enabled runtime module lists that executable in `provides`. ACP agent packages implement `IAgentCapability`; Server lists them from enabled agent-kind modules by module id and wraps each launch in that module's Nix environment. When a packed DLL is missing, the manifest `Launch` template remains a fallback so enablement still works. There is no second non-Nix install path. Native Windows Server hosts must use WSL2/Linux.

<DocNext href="/docs/configuration/reference" title="User reference">
The JSON field contract.
</DocNext>
