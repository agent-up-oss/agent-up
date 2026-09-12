namespace AgentUp.Server.Features.Processes.DTOs;

public sealed record WorkspaceRuntimeSnapshot(double CpuPercent, long MemoryBytes, int ProcessCount)
{
    public static WorkspaceRuntimeSnapshot Empty { get; } = new(0, 0, 0);
}
