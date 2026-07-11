namespace AgentUp.CLI.Models;

public record ApplicationDefinition(
    string Name,
    string Command,
    string? Path,
    string? PortVariable)
{
    public string? State { get; init; }
}
