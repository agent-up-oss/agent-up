namespace AgentUp.Server.Features.Capabilities.DTOs;

public enum CapabilityChangeStatus
{
    Changed,
    NotFound,

    /// <summary>The software the capability runs was not found, so it cannot be enabled.</summary>
    Unavailable,

    /// <summary>A setting value was rejected.</summary>
    Invalid
}
