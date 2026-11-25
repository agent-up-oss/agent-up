namespace AgentUp.Desktop.Features.Interface.Models;

/// <summary>
/// One task in the Server's queue. The agents work these and only these, in this order. The stage,
/// proposal, reviews and reviewer slots are absent from Servers that predate the team pipeline.
/// </summary>
public sealed record QueuedTask(
    string Id,
    string Owner,
    string Repository,
    int Number,
    string Title,
    string Url,
    string? TargetWorkspaceId,
    string? HeldByWorkspaceId,
    string? Stage = null,
    QueuedTaskProposal? Proposal = null,
    IReadOnlyList<QueuedTaskReview>? Reviews = null,
    string? DevReviewerWorkspaceId = null,
    string? PmReviewerWorkspaceId = null,
    IReadOnlyList<string>? NeedsReview = null);

/// <summary>The proposal an author's work became; each push is the next revision.</summary>
public sealed record QueuedTaskProposal(string Number, string Url, string Branch, string BaseBranch, int Revision, string? Head = null);

/// <summary>One reviewer's verdict on one revision, and which kind of review it was.</summary>
public sealed record QueuedTaskReview(string WorkspaceId, string Verdict, int Revision, string Body, string Kind = "devReviewer");

/// <summary>An open issue on a workspace's repository, offered for picking.</summary>
public sealed record OfferedIssue(string Id, int Number, string Title, string Url);

/// <summary>
/// The issues on offer and where a pick is taken from. <see cref="PickFrom"/> is null when the
/// Server's task source offered them: the Server then picks from that source, not from a workspace.
/// </summary>
public sealed record OfferedIssues(
    IReadOnlyList<OfferedIssue> Issues,
    string? PickFrom,
    string? ModuleId = null,
    string? Owner = null,
    string? Repository = null);
