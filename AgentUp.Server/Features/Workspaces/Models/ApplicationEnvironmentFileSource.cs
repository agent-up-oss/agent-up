namespace AgentUp.Server.Features.Workspaces.Models;

public sealed record ApplicationEnvironmentFileSource(
    string Name,
    IReadOnlyList<string>? EnvironmentFiles);
