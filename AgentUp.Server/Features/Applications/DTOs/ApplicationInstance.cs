namespace AgentUp.Server.Features.Applications.DTOs;

public class ApplicationInstance
{
    public required string Name { get; init; }
    public ServiceType ServiceType { get; init; } = ServiceType.Process;

    // Process fields
    public string? Command { get; init; }
    public string? Path { get; init; }
    public string? PortVariable { get; init; }

    // Docker fields
    public string? Image { get; init; }
    public IReadOnlyList<string>? Ports { get; init; }
    public IReadOnlyDictionary<string, string>? Environment { get; init; }
    public IReadOnlyList<string>? Volumes { get; init; }

    public ApplicationState State { get; set; } = ApplicationState.Stopped;
}
