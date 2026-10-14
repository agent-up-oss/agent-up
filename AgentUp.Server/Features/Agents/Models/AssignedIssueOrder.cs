namespace AgentUp.Server.Features.Agents.Models;

/// <summary>Declared deterministic order for the assigned-issue queue. Not the agent's judgement.</summary>
public enum AssignedIssueOrder
{
    NumberAsc,
    CreatedAsc,
    UpdatedAsc
}
