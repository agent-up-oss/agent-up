namespace AgentUp.Server.Features.Commits.Models;

public sealed record GitOperationState(string Kind, bool Blocking)
{
    public static GitOperationState None { get; } = new("none", false);
}
