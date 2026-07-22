namespace AgentUp.CLI.Features.Workspaces.DTOs;

public record ApplicationDefinition(
    string Name,
    string Command,
    string? Path,
    IReadOnlyList<PortDeclaration>? Ports = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? EnvironmentFiles = null)
{
    public string? State { get; init; }
}
