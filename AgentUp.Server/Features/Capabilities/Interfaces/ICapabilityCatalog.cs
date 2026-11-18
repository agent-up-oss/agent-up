using AgentUp.Sdk.Common;

namespace AgentUp.Server.Features.Capabilities.Interfaces;

/// <summary>Every capability compiled into this Server, enabled or not.</summary>
public interface ICapabilityCatalog
{
    IReadOnlyList<ICapability> Modules { get; }
}
