using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>
/// The Git and host work around one team member's turn on a task: getting the clone ready and
/// saying what to do before, and turning what the agent produced into a proposal or a review after.
/// </summary>
public interface ITaskPipeline
{
    Task<TaskTurnPlan> PrepareAsync(string workspaceId, AgentRoleDefinition role, AssignedIssue task, CancellationToken cancellationToken);

    Task<TaskTurnOutcome> FinishAsync(string workspaceId, AgentRoleDefinition role, AssignedIssue task, LoopRunResult result, CancellationToken cancellationToken);
}
