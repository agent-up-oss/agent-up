namespace AgentUp.CLI.Models;

public record DockerServiceDefinition(
    string Name,
    string Image,
    IReadOnlyList<string>? Ports = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? Volumes = null);
