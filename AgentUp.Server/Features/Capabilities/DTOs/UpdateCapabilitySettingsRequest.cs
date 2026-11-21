namespace AgentUp.Server.Features.Capabilities.DTOs;

/// <param name="Values">
/// Settings to change, by key. A key left out keeps its stored value, so a client can save a
/// form without resending a secret it was never shown; an empty string clears the value.
/// </param>
public sealed record UpdateCapabilitySettingsRequest(IReadOnlyDictionary<string, string?> Values);
