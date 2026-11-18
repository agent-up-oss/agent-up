namespace AgentUp.Server.Features.Capabilities.DTOs;

/// <param name="Value">The stored value, or null for a secret: a secret is never returned.</param>
/// <param name="HasValue">Whether a value is stored, which is how a client tells a set secret from an empty one.</param>
public sealed record CapabilitySettingDto(
    string Key,
    string Label,
    string Kind,
    bool Required,
    string? Placeholder,
    string? Value,
    bool HasValue);
