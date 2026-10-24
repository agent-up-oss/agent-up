namespace AgentUp.CLI.Features.Capabilities.DTOs;

public sealed record CapabilitiesCommand(
    string Verb,
    string? Id = null,
    string? Version = null);
