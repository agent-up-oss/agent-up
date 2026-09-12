namespace AgentUp.Server.Features.Agents.Models;

public sealed record PendingAgentPermission(
    IReadOnlySet<string> OptionIds,
    TaskCompletionSource<string> Completion);
