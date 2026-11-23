namespace AgentUp.Server.Features.Agents.DTOs;

/// <summary>
/// One picked task, as the Tasks tab shows it. The two reviewer ids are who holds each kind of
/// review for every revision; <paramref name="NeedsReview"/> names the kinds -
/// <c>devReviewer</c>, <c>pmReviewer</c> - the open revision still waits on.
/// </summary>
public sealed record QueuedTaskDto(
    string Id,
    string Owner,
    string Repository,
    int Number,
    string Title,
    string Body,
    string Url,
    DateTimeOffset QueuedAt,
    string? TargetWorkspaceId,
    string? HeldByWorkspaceId,
    string Stage = "queued",
    TaskProposalDto? Proposal = null,
    IReadOnlyList<TaskReviewDto>? Reviews = null,
    string? DevReviewerWorkspaceId = null,
    string? PmReviewerWorkspaceId = null,
    IReadOnlyList<string>? NeedsReview = null);

/// <summary>The proposal a task's author opened, and which revision of it is under review.</summary>
/// <summary>A proposal revision; <paramref name="Head"/> is the commit that revision is.</summary>
public sealed record TaskProposalDto(string Number, string Url, string Branch, string BaseBranch, int Revision, string? Head = null);

/// <summary>
/// One reviewer's verdict on one revision: <c>approve</c>, <c>requestChanges</c> or <c>comment</c>,
/// and whether it was a <c>devReviewer</c> or <c>pmReviewer</c> review.
/// </summary>
public sealed record TaskReviewDto(string WorkspaceId, string Verdict, int Revision, string Body, string Kind = "devReviewer");

/// <summary>Picks one of the repository's open issues into the queue.</summary>
/// <summary>
/// Picks an issue into the queue. WorkspaceId names the workspace whose repository the issue is
/// on, and is left out once the Server has its own task source: then the issue comes from there,
/// which is one backlog for the whole team rather than one per workspace.
/// </summary>
public sealed record EnqueueTaskRequest(
    string? WorkspaceId,
    string IssueId,
    string? TargetWorkspaceId = null,
    string? ModuleId = null,
    string? Owner = null,
    string? Repository = null);

/// <summary>Moves a task to a position in the queue, counted from zero.</summary>
public sealed record MoveTaskRequest(int Index);

/// <summary>Points a task at one workspace, or at anyone when the id is null.</summary>
public sealed record TargetTaskRequest(string? WorkspaceId);
