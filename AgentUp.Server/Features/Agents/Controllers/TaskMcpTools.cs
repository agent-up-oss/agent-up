using System.ComponentModel;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Agents.Controllers;

/// <summary>
/// The task tools on <c>/mcp/tasks</c>. These are the only writes an agent makes about its task:
/// committing, pushing, opening the proposal, assigning, requesting reviewers and posting the
/// review are the Server's, done on the member's behalf with the member's own credential.
/// </summary>
[McpServerToolType]
public sealed class TaskMcpTools(TaskWorkController work)
{
    [McpServerTool(Name = "get_task", Title = "Read the task you are on")]
    [Description(
        "Read the task you are working on: the issue, its stage, the proposal and revision, every review of the open revision, "
        + "review findings at their file and line, the latest result of each check with a link to its log, the comments, and what to do next. "
        + "Call it first, and again after a revision comes back to you.")]
    public async Task<McpToolResult> GetTask(
        [Description("The workspace you are working in.")]
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var task = await work.DescribeAsync(workspaceId, cancellationToken);
        return task is null
            ? new McpToolResult(false, "You hold no task right now.")
            : new McpToolResult(true, $"Issue #{task.Number}: {task.Title} ({task.Stage}). {task.Next}", task);
    }

    [McpServerTool(Name = "submit_work", Title = "Submit your change for review")]
    [Description(
        "Authors only. Call once when the files are changed and the task is done. Agent-Up commits your changes to the task branch, "
        + "pushes it and opens or updates the proposal, then the reviewers are asked. Do not run git commit or git push yourself. "
        + "summary is what you changed and why, in a few sentences; it becomes the commit message and the proposal description.")]
    public async Task<McpToolResult> SubmitWork(
        [Description("The workspace you are working in.")]
        string workspaceId,
        [Description("What you changed and why, in a few sentences.")]
        string summary,
        CancellationToken cancellationToken = default)
    {
        var outcome = await work.SubmitWorkAsync(workspaceId, summary, cancellationToken);
        return new McpToolResult(outcome.Done, outcome.Message, outcome);
    }

    [McpServerTool(Name = "submit_review", Title = "Submit your review")]
    [Description(
        "Reviewers only. Call once with your verdict on the revision you are reviewing; it is posted on the proposal under your account "
        + "and your workspace is stopped. verdict is approve or request_changes. summary is what you checked and what you found. "
        + "findings are problems in the code, each at a file path and line of the change with a comment. "
        + "screenshotArtifactIds are the ids browser_screenshot returned, attached to the review so the author sees what you saw.")]
    public async Task<McpToolResult> SubmitReview(
        [Description("The workspace you are working in.")]
        string workspaceId,
        [Description("approve or request_changes.")]
        string verdict,
        [Description("What you checked and what you found.")]
        string summary,
        [Description("Problems in the code: each a path, a line of the changed file, and a comment.")]
        IReadOnlyList<TaskFindingDto>? findings = null,
        [Description("Screenshot artifact ids from browser_screenshot, at most six.")]
        IReadOnlyList<string>? screenshotArtifactIds = null,
        CancellationToken cancellationToken = default)
    {
        var outcome = await work.SubmitReviewAsync(workspaceId, verdict, summary, findings, screenshotArtifactIds, cancellationToken);
        return new McpToolResult(outcome.Done, outcome.Message, outcome);
    }
}
