namespace AgentUp.Server.Features.Applications.DTOs;

public record DotnetRunDefinition(
    string Project,
    IReadOnlyList<string>? Arguments = null);
