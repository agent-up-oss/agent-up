namespace AgentUp.Desktop.Features.Workspaces.DTOs;

public sealed record CloneSourceRequestDto(string Repository, string Branch)
{
    public string? Revision { get; init; }

    public bool Derived { get; init; }
}
