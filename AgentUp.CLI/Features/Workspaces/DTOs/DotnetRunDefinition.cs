namespace AgentUp.CLI.Features.Workspaces.DTOs;

public record DotnetRunDefinition(
    string Project,
    IReadOnlyList<string>? Arguments = null);
