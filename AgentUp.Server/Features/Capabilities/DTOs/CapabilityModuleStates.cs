namespace AgentUp.Server.Features.Capabilities.DTOs;

public static class CapabilityModuleStates
{
    /// <summary>Software the capability runs was not found. Enabling is refused.</summary>
    public const string Unavailable = "unavailable";

    public const string Disabled = "disabled";

    /// <summary>Enabled, but a required setting has no value.</summary>
    public const string NeedsSetup = "needsSetup";

    public const string Ready = "ready";
}
