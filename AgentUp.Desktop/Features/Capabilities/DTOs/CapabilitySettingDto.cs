namespace AgentUp.Desktop.Features.Capabilities.DTOs;

/// <param name="Value">The stored value; always null for a secret, which the Server never returns.</param>
public sealed record CapabilitySettingDto(
    string Key,
    string Label,
    string Kind,
    bool Required,
    string? Placeholder,
    string? Value,
    bool HasValue);
