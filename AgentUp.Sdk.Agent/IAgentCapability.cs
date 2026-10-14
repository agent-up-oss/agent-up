using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Agent;

public interface IAgentCapability
{
    CapabilityIdentity Identity { get; }

    IReadOnlyList<NixPackageDeclaration> NixPackages { get; }

    bool CanRun { get; }

    /// <summary>
    /// Whether this module emits ACP <c>session/request_permission</c>. A module that never
    /// asks cannot be governed by a workspace permission policy, so autonomy is refused for it.
    /// </summary>
    bool SupportsPermissionRequests { get; }

    AgentLaunchResult Launch();

    AgentLoginSpec? Login { get; }
}
