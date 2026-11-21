using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Services;

namespace AgentUp.Server.Features.Agents.Controllers;

/// <summary>The task an agent is on, and how it says it is done. MCP is the only caller.</summary>
public sealed class TaskWorkController(TaskWorkService work)
{
    public Task<TaskBriefDto?> DescribeAsync(string workspaceId, CancellationToken cancellationToken = default)
        => work.DescribeAsync(workspaceId, cancellationToken);

    public Task<TaskSubmissionDto> SubmitWorkAsync(string workspaceId, string? summary, CancellationToken cancellationToken = default)
        => work.SubmitWorkAsync(workspaceId, summary, cancellationToken);

    public Task<TaskSubmissionDto> SubmitReviewAsync(
        string workspaceId,
        string verdict,
        string summary,
        IReadOnlyList<TaskFindingDto>? findings,
        IReadOnlyList<string>? screenshotArtifactIds,
        CancellationToken cancellationToken = default)
        => work.SubmitReviewAsync(workspaceId, verdict, summary, findings, screenshotArtifactIds, cancellationToken);
}
