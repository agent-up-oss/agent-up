using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Runtime;

namespace AgentUp.Server.Features.Capabilities.DTOs;

public sealed record CapabilityLoadedModule(IRuntimeCapability? Runtime, IAgentCapability? Agent);
