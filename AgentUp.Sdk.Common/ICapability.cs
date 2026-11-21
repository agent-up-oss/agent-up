namespace AgentUp.Sdk.Common;

/// <summary>
/// What every first-party capability declares, whichever kind it is. The Server compiles every
/// capability in, so this is not an install contract: it is what the Server needs to decide
/// whether a capability can be enabled on this host and what a user has to configure for it.
/// </summary>
public interface ICapability
{
    CapabilityIdentity Identity { get; }

    /// <summary>One of <see cref="CapabilityKind.Allowed"/>.</summary>
    string Kind { get; }

    /// <summary>
    /// The software this capability runs. While any of it cannot be found on the Server host the
    /// capability cannot be enabled, and an enabled one stops offering its feature.
    /// </summary>
    IReadOnlyList<CapabilitySoftware> Software { get; }

    /// <summary>Values a user configures once the capability is enabled. Empty for most capabilities.</summary>
    IReadOnlyList<CapabilitySettingSpec> Settings { get; }
}
