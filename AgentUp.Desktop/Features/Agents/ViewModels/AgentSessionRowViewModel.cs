using AgentUp.Desktop.Features.Agents.DTOs;

namespace AgentUp.Desktop.Features.Agents.ViewModels;

public sealed class AgentSessionRowViewModel(AgentSessionSummaryDto session, bool isCurrent, string state)
{
    public string SessionId { get; } = session.SessionId;
    public string Agent { get; } = session.Agent;
    public string Description { get; } = session.Description;
    public string Branch { get; } = session.Branch;
    public DateTimeOffset LastUsedAt { get; } = session.LastUsedAt;
    public bool IsCurrent { get; } = isCurrent;
    public string ActionLabel { get; } = isCurrent ? $"Current · {state}" : "Resume session →";
}
