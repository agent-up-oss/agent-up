using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Agent;

public interface IAgentCapability : ICapability
{
    IReadOnlyList<NixPackageDeclaration> NixPackages { get; }

    /// <summary>
    /// Whether this module emits ACP <c>session/request_permission</c>. A module that never
    /// asks cannot be governed by a workspace permission policy, so autonomy is refused for it.
    /// </summary>
    bool SupportsPermissionRequests { get; }

    /// <summary>
    /// The ACP process to start. <paramref name="settings"/> holds the values a user configured
    /// for this capability, secrets included, keyed by <see cref="CapabilitySettingSpec.Key"/>.
    /// </summary>
    AgentLaunchResult Launch(IReadOnlyDictionary<string, string> settings);

    AgentLoginSpec? Login { get; }
}
