using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Agent;

public interface IAgentCapability
{
    CapabilityIdentity Identity { get; }

    IReadOnlyList<NixPackageDeclaration> NixPackages { get; }

    bool CanRun { get; }

    AgentLaunchResult Launch();

    AgentLoginSpec? Login { get; }
}
