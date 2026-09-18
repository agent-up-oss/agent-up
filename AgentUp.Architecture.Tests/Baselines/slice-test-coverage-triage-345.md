# Slice coverage debt triage for issue 345

The stopping condition was zero structural debt entries, but an entry was removed only after
executable tests exercised the production behavior in its owning slice. The table groups entries
that share one measured slice percentage; **Entries** totals all 35 original baseline lines.
Coverage is from the owning suite's Cobertura report after the burn-down. Authentication and
Desktop Browser remain deliberate, pre-existing slice exemptions; no exemption was added.

| Coverage | Slice | Entries | Decision and coverage added |
|---:|---|---:|---|
| 65.2% | `AgentUp.CLI/Features/Authentication` | 2 | Write tests: success and failure output behavior; retain the existing exemption. |
| 57.6% | `AgentUp.Desktop/Features/Browser` | 1 | Write tests: script generation/escaping and viewport delegation; retain the existing exemption. |
| 73.7% | `AgentUp.Server/Features/Processes` | 1 | Write tests: workspace/application lifecycle, output, and runtime delegation. |
| 75.2% | `AgentUp.Server/Features/Validation` | 1 | Write tests: MCP save and replay guidance contracts. |
| 76.7% | `AgentUp.Capabilities.Common/Features/CapabilityDistribution` | 1 | Write tests: cache planning and version isolation. |
| 86.1% | `AgentUp.Capabilities.Common/Features/CapabilityDiscovery` | 1 | Write tests: command result data and value semantics. |
| 82.8% | `AgentUp.Desktop/Features/FirstRun` | 2 | Write tests: controller initialization and result-state behavior. |
| 80.6% | `AgentUp.Desktop/Features/Workspaces` | 1 | Write tests: clone request mapping and lifecycle delegation. |
| 88.0% | `AgentUp.Desktop/Features/Audit` | 1 | Write tests: unattached and repeated disposal behavior. |
| 88.5% | `AgentUp.Server/Features/Applications` | 1 | Write tests: unknown health queries and health lifecycle behavior. |
| 88.7% | `AgentUp.Desktop/Features/Database` | 1 | Write tests: result mapping and escaped boundary arguments. |
| 89.0% | `AgentUp.Desktop/Features/Metrics` | 1 | Write tests: metric shape and non-negative sampled values. |
| 89.7% | `AgentUp.CLI/Features/Workspaces` | 2 | Write tests: configuration/identity models, resolution, and output behavior. |
| 90.6% | `AgentUp.Desktop/Features/Applications` | 1 | Write tests: application normalization identity/order and empty state. |
| 91.9% | `AgentUp.Desktop/Features/Ports` | 2 | Write tests: declared-port mapping and operational tab ordering. |
| 100.0% | `AgentUp.Server/Features/Ports` | 4 | Write tests: allocation/recycling/conflicts, persistence, sockets, and controller delegation. |
| 94.2% | `AgentUp.Desktop/Features/Validation` | 2 | Write tests: load boundary values, replacement filters, and export URI escaping. |
| 94.4% | `AgentUp.Server/Features/Agents` | 1 | Write tests: blank prompts execute controller validation before scheduling. |
| 94.8% | `AgentUp.Server/Features/Workspaces` | 1 | Write tests: registration/query behavior and state transitions. |
| 95.9% | `AgentUp.Capabilities.Common/Features/CapabilityInventory` | 1 | Write tests: minimal declarations and launch overrides. |
| 97.6% | `AgentUp.Desktop/Features/Console` | 2 | Write tests: API routing/deserialization, cancellation, and controller output. |
| 98.9% | `AgentUp.Desktop/Features/Agents` | 1 | Write tests: command delegation plus missing get/schedule behavior. |
| 98.9% | `AgentUp.Server/Features/Capabilities` | 2 | Write tests: missing-adapter reconciliation and controller contracts for .NET and Docker. |
| 100.0% | `AgentUp.Server/Features/Metrics` | 2 | Write tests: sampled values plus hosted-service recording and cancellation. |
