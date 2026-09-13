namespace AgentUp.Server.Features.ApplicationProxy.Models;

public sealed class ApplicationProxySession
{
    public required string WorkspaceId { get; init; }
    public required int AllocatedPort { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
