namespace AgentUp.CLI.Models;

public record ApplicationDefinition(
    string Name,
    string Command,
    string? Path,
    IReadOnlyList<PortDeclaration>? Ports = null)
{
    public string? State { get; init; }
}
