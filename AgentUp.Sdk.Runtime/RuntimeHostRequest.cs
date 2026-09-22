namespace AgentUp.Sdk.Runtime;

public sealed record RuntimeHostRequest(
    string Name,
    string? TechnologyVersion,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<RuntimePortMapping> Ports,
    IReadOnlyList<string> Volumes,
    IReadOnlyList<string> ExtraArguments,
    string WorkspaceId,
    string ContainerName,
    IReadOnlyList<string>? EnvironmentFilePaths = null,
    string? AuditEndpoint = null);
