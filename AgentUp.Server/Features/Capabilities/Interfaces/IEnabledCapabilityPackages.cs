using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Capabilities.DTOs;

namespace AgentUp.Server.Features.Capabilities.Interfaces;

public interface IEnabledCapabilityPackages
{
    CapabilityPackageManifest? GetEnabled(string id);

    CapabilityLaunchPlan? AgentLaunch(string id);

    CapabilityLaunchWrapDto WrapLaunch(string fileName, IReadOnlyList<string> arguments);

    CapabilityLaunchWrapDto WrapModule(string id, string fileName, IReadOnlyList<string> arguments);

    IRuntimeCapability? GetRuntime(string id);

    IReadOnlyList<IRuntimeCapability> ListRuntimes();

    IAgentCapability? GetAgent(string id);

    IReadOnlyList<IAgentCapability> ListAgents();
}
