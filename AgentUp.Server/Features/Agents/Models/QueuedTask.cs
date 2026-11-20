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
    string? BaseBranch = null)
{
    /// <summary>The reviews of the revision now open, which are the only ones that decide anything.</summary>
    [JsonIgnore]
    public IReadOnlyList<TaskReview> CurrentReviews =>
        Proposal is null ? [] : [.. (Reviews ?? []).Where(review => review.Revision == Proposal.Revision)];

    /// <summary>Whether this reviewer still owes a review of the revision now open.</summary>
    public bool AwaitsReviewFrom(string workspaceId) =>
        Stage == TaskStage.InReview
        && CurrentReviews.All(review => !string.Equals(review.WorkspaceId, workspaceId, StringComparison.Ordinal));

    /// <summary>Whether this workspace may take the task, ignoring who already holds it.</summary>
    public bool OfferedTo(string workspaceId, string owner, string repository) =>
        string.Equals(Owner, owner, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Repository, repository, StringComparison.OrdinalIgnoreCase)
        && (TargetWorkspaceId is null || string.Equals(TargetWorkspaceId, workspaceId, StringComparison.Ordinal));
}
