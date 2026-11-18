using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Forge;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Capabilities.DTOs;

namespace AgentUp.Server.Features.Capabilities.Interfaces;

/// <summary>
/// The capabilities other slices consume. <c>Get*</c> and <c>List*</c> return enabled
/// capabilities only; <see cref="Readiness"/> says whether an enabled one can actually run.
/// </summary>
public interface IEnabledCapabilityPackages
{
    CapabilityLaunchPlan? AgentLaunch(string id);

    CapabilityLaunchWrapDto WrapLaunch(string fileName, IReadOnlyList<string> arguments);

    CapabilityLaunchWrapDto WrapModule(string id, string fileName, IReadOnlyList<string> arguments);

    IRuntimeCapability? GetRuntime(string id);

    IReadOnlyList<IRuntimeCapability> ListRuntimes();

    IAgentCapability? GetAgent(string id);

    IReadOnlyList<IAgentCapability> ListAgents();

    /// <summary>Every agent capability compiled into the Server, enabled or not.</summary>
    IReadOnlyList<IAgentCapability> AllAgents();

    IForgeCapability? GetForge(string id);

    IReadOnlyList<IForgeCapability> ListForges();

    /// <summary>Whether a capability is enabled, and what it still lacks; null for an unknown id.</summary>
    CapabilityReadinessDto? Readiness(string id);
}
