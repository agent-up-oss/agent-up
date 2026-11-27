using System.Text.Json.Serialization;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// One issue somebody picked for the agents to work on. Nothing reaches an agent by being open on
/// the host: an issue is worked on because it is here, in the order it sits here.
/// </summary>
/// <param name="Id">The host's own issue id, unique within the repository.</param>
/// <param name="TargetWorkspaceId">
/// The one workspace allowed to take this task, or null to let whichever workspace on the
/// repository asks next take it. Hosts that cannot assign an agent are the reason this is local.
/// </param>
/// <param name="HeldByWorkspaceId">
/// The workspace that took this task, or null while it is still free. A hold is sticky: it
/// survives a block, a failure and a Server restart, so the workspace that started a task is the
/// one that carries it on.
/// </param>
/// <param name="Stage">Where the task is between author and reviewers.</param>
/// <param name="Proposal">The proposal the author's work became, once there is one.</param>
/// <param name="Reviews">Every review of every revision, oldest first.</param>
/// <param name="BaseBranch">The branch the author started from, which the proposal targets.</param>
/// <param name="DevReviewerWorkspaceId">
/// The dev reviewer that took this task's review. Sticky like the author's hold: every later
/// revision goes back to the same reviewer rather than to whoever asks first.
/// </param>
/// <param name="PmReviewerWorkspaceId">The product reviewer that took this task's review, sticky the same way.</param>
public sealed record QueuedTask(
    string Id,
    string Owner,
    string Repository,
    int Number,
    string Title,
    string Body,
    string Url,
    DateTimeOffset QueuedAt,
    string? TargetWorkspaceId = null,
    string? HeldByWorkspaceId = null,
    TaskStage Stage = TaskStage.Queued,
    TaskProposal? Proposal = null,
    IReadOnlyList<TaskReview>? Reviews = null,
    string? BaseBranch = null,
    string? DevReviewerWorkspaceId = null,
    string? PmReviewerWorkspaceId = null)
{
    /// <summary>The reviews of the revision now open, which are the only ones that decide anything.</summary>
    [JsonIgnore]
    public IReadOnlyList<TaskReview> CurrentReviews =>
        Proposal is null ? [] : [.. (Reviews ?? []).Where(review => review.Revision == Proposal.Revision)];

    /// <summary>The reviewer of this kind the task is with, or null while nobody of that kind took it.</summary>
    public string? ReviewerFor(TeamMemberKind kind) => kind switch
    {
        TeamMemberKind.DevReviewer => DevReviewerWorkspaceId,
        TeamMemberKind.PmReviewer => PmReviewerWorkspaceId,
        _ => null
    };

    /// <summary>The same task with this reviewer holding its review of this kind.</summary>
    public QueuedTask WithReviewer(TeamMemberKind kind, string workspaceId) => kind switch
    {
        TeamMemberKind.DevReviewer => this with { DevReviewerWorkspaceId = workspaceId },
        TeamMemberKind.PmReviewer => this with { PmReviewerWorkspaceId = workspaceId },
        _ => this
    };

    /// <summary>
    /// Whether the revision now open still needs a review of this kind: nobody of the kind has
    /// reviewed it yet. A host review copied onto an adopted pull has no Agent-Up workspace
    /// holder, so kind is what settles it. A push opens a new revision, so every push needs
    /// every kind again.
    /// </summary>
    public bool NeedsReview(TeamMemberKind kind) =>
        Stage == TaskStage.InReview
        && !CurrentReviews.Any(review => review.Kind == kind);

    /// <summary>
    /// Whether this reviewer is offered the revision now open: it needs a review of the reviewer's
    /// kind and is either still free or already this reviewer's.
    /// </summary>
    public bool AwaitsReviewFrom(string workspaceId, TeamMemberKind kind) =>
        NeedsReview(kind)
        && (ReviewerFor(kind) is null || string.Equals(ReviewerFor(kind), workspaceId, StringComparison.Ordinal));

    /// <summary>Whether this workspace may take the task, ignoring who already holds it.</summary>
    public bool OfferedTo(string workspaceId, string owner, string repository) =>
        string.Equals(Owner, owner, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Repository, repository, StringComparison.OrdinalIgnoreCase)
        && (TargetWorkspaceId is null || string.Equals(TargetWorkspaceId, workspaceId, StringComparison.Ordinal));
}
