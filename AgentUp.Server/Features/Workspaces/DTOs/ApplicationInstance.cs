namespace AgentUp.Server.Features.Workspaces.DTOs;

public class ApplicationInstance
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public string? Path { get; init; }
    public string? PortVariable { get; init; }
    public ApplicationState State { get; set; } = ApplicationState.Stopped;
}
