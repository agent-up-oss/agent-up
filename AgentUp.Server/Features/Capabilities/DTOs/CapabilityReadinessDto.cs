namespace AgentUp.Server.Features.Capabilities.DTOs;

/// <param name="MissingSoftware">Executables, or <c>nix</c>, that could not be found on the Server host.</param>
/// <param name="MissingSettings">Labels of required settings with no value.</param>
public sealed record CapabilityReadinessDto(
    string Id,
    string DisplayName,
    bool Enabled,
    IReadOnlyList<string> MissingSoftware,
    IReadOnlyList<string> MissingSettings)
{
    public bool Available => MissingSoftware.Count == 0;

    public bool CanRun => Enabled && Available && MissingSettings.Count == 0;
}
