namespace AgentUp.Desktop.Features.Capabilities.DTOs;

/// <param name="Values">Only the settings to change; a key left out keeps its stored value.</param>
public sealed record UpdateCapabilitySettingsRequestDto(IReadOnlyDictionary<string, string> Values);
