using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Server.Features.Capabilities.DTOs;

internal sealed record ResolvedCapability(CapabilityLaunchPlan Plan, CapabilityStatusDto Status);
