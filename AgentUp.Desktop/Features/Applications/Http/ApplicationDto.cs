namespace AgentUp.Desktop.Features.Applications.Http;

public sealed record ApplicationDto(
    string Name,
    string Command,
    string? Path,
    string? PortVariable,
    string State);
