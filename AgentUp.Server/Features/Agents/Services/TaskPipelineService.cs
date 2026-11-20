using System.Net.Http;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// Moves a picked task between team members through the host. Agents only change files and say
/// what they think; the Server makes every Git and host write around them, with the member's own
/// credential, so an author's work becomes a proposal on its task branch and a reviewer's verdict
/// becomes a review on that proposal, both attributed to the member that did the work.
/// </summary>
public sealed class TaskPipelineService(
    TaskQueueService queue,
    ForgeController forge,
    GitWorkspaceController git,
    GitQueryController gitRead,
    WorkspaceQueryController workspaces,
    IAgentWorkspaceSettingsRepository settings,
    IRoleProgress progress,
    IReviewApplicationHost? applications = null) : ITaskPipeline
{
    public async Task<TaskTurnPlan> PrepareAsync(string workspaceId, AgentRoleDefinition role, AssignedIssue issue, CancellationToken cancellationToken)
    {
        var task = Find(workspaceId, issue.Id);
        if (task is null)
            return TaskTurnPlan.Refused("The task is no longer in the queue.");
        return role.Trigger == RoleTriggerKind.ProposalAwaitingReview
            ? await PrepareReviewAsync(workspaceId, role, task, cancellationToken)
            : await PrepareAuthorAsync(workspaceId, role, task, cancellationToken);
    }

    public async Task<TaskTurnOutcome> FinishAsync(string workspaceId, AgentRoleDefinition role, AssignedIssue issue, LoopRunResult result, CancellationToken cancellationToken)
    {
        var task = Find(workspaceId, issue.Id);
        if (task is null)
            return TaskTurnOutcome.Blocked("task_missing", "The task left the queue while it was being worked.");
        try
        {
            return role.Trigger == RoleTriggerKind.ProposalAwaitingReview
                ? await SubmitReviewAsync(workspaceId, task, result, cancellationToken)
                : await PublishAsync(workspaceId, task, result, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return TaskTurnOutcome.Blocked("host_unavailable", exception.Message);
        }
    }

    /// <summary>
    /// A fresh task starts from its base branch; one sent back by review continues on its task
    /// branch, where the earlier revision already is.
    /// </summary>
    private async Task<TaskTurnPlan> PrepareAuthorAsync(string workspaceId, AgentRoleDefinition role, QueuedTask task, CancellationToken cancellationToken)
    {
        if (task.Stage == TaskStage.Queued)
        {
            var head = await gitRead.GetHeadAsync(workspaceId, cancellationToken);
            var current = head?.Branch ?? string.Empty;
            var baseBranch = current.Length > 0 && !current.StartsWith(ForgeController.TaskBranchPrefix, StringComparison.Ordinal) && current != "HEAD"
                ? current
                : PreviousBase(workspaceId) ?? "main";
            if (current != baseBranch)
            {
                var moved = await CheckoutAsync(workspaceId, baseBranch, [baseBranch], cancellationToken);
                if (moved is not null)
                    return TaskTurnPlan.Refused(moved);
            }
            queue.Begin(workspaceId, task.Owner, task.Repository, task.Id, baseBranch);
            task = Find(workspaceId, task.Id)!;
        }

        return TaskTurnPlan.Ready(TaskPrompts.Author(role, task));
    }

    /// <summary>A reviewer reads exactly the revision under review, fetched with its own credential.</summary>
    private async Task<TaskTurnPlan> PrepareReviewAsync(string workspaceId, AgentRoleDefinition role, QueuedTask task, CancellationToken cancellationToken)
    {
        if (task.Proposal is null)
            return TaskTurnPlan.Refused("The task has no proposal to review.");
        var checkout = await CheckoutAsync(workspaceId, task.Proposal.Branch, [task.Proposal.Branch, task.Proposal.BaseBranch], cancellationToken);
        if (checkout is not null)
            return TaskTurnPlan.Refused(checkout);
        // A product reviewer never reads the repository: it is told what was asked and tries the
        // checked-out revision in the running application instead of reading a diff.
        if (settings.Get(workspaceId).Member == TeamMemberKind.PmReviewer)
        {
            IReadOnlyList<string> addresses = [];
            if (applications is not null)
            {
                var (running, error) = await applications.RunAsync(workspaceId, cancellationToken);
                if (error is not null)
                    return TaskTurnPlan.Refused($"The revision under review did not start: {error}");
                addresses = running;
            }

            return TaskTurnPlan.Ready(TaskPrompts.ProductReviewer(role, task, addresses));
        }
        var diff = await gitRead.GetDiffAsync(workspaceId, $"refs/remotes/origin/{task.Proposal.BaseBranch}", "HEAD", cancellationToken);
        return TaskTurnPlan.Ready(TaskPrompts.Reviewer(role, task, diff));
    }

    private async Task<TaskTurnOutcome> PublishAsync(string workspaceId, QueuedTask task, LoopRunResult result, CancellationToken cancellationToken)
    {
        var branch = task.Proposal?.Branch ?? $"{ForgeController.TaskBranchPrefix}issue-{task.Number}";
        var baseBranch = task.Proposal?.BaseBranch ?? task.BaseBranch ?? "main";
        var committed = await git.CommitAllToBranchAsync(workspaceId, branch, TaskPrompts.Commit(task, result.Reply), Identity(workspaceId), cancellationToken);
        // A commit already on the task branch from an attempt whose push failed is still the
        // work to publish; only a branch with no proposal yet can be in that state.
        if (committed.NothingToCommit
            && (task.Proposal is not null || (await gitRead.GetHeadAsync(workspaceId, cancellationToken))?.Branch != branch))
            return TaskTurnOutcome.Blocked("nothing_to_propose", "The agent finished without changing any file, so there is nothing to propose.");
        if (!committed.Succeeded && !committed.NothingToCommit)
            return TaskTurnOutcome.Blocked("commit_failed", committed.Error ?? "The commit failed.");

        var pushed = await forge.PushTaskBranchAsync(workspaceId, branch, cancellationToken);
        if (!pushed.Succeeded)
            return TaskTurnOutcome.Blocked("push_failed", pushed.Error ?? "The push failed.");
        var head = await gitRead.ResolveCommitAsync(workspaceId, "HEAD", cancellationToken);

        string number;
        string url;
        if (task.Proposal is null)
        {
            var opened = await forge.OpenProposalAsync(
                workspaceId,
                new ForgeOpenProposalRequest(task.Title, TaskPrompts.ProposalBody(task, result.Reply), branch, baseBranch),
                cancellationToken);
            if (opened is null)
                return TaskTurnOutcome.Blocked("proposal_failed", "The host did not open the proposal.");
            (number, url) = (opened.Number, opened.Url);
        }
        else
        {
            (number, url) = (task.Proposal.Number, task.Proposal.Url);
            if (!string.IsNullOrWhiteSpace(result.Reply))
                await forge.CommentAsync(workspaceId, new ForgeCommentRequest(number, result.Reply.Trim()), cancellationToken);
        }

        queue.Propose(workspaceId, task.Owner, task.Repository, task.Id, number, url, branch, baseBranch, head);
        progress.Record(workspaceId, RoleTerminationKind.ProposalOpened);
        return TaskTurnOutcome.Done();
    }

    private async Task<TaskTurnOutcome> SubmitReviewAsync(string workspaceId, QueuedTask task, LoopRunResult result, CancellationToken cancellationToken)
    {
        var (verdict, body) = ReviewVerdictText.Read(result.Reply);
        // A reply with no verdict is not a review: posting it would put words on the proposal under
        // the reviewer's name that decide nothing, and it would take the task off their list.
        if (verdict == TaskReviewVerdict.Comment && !progress.Satisfied(workspaceId, RoleTerminationKind.ReviewSubmitted))
            return TaskTurnOutcome.Blocked("no_verdict", "The review did not end with VERDICT: APPROVE or VERDICT: REQUEST_CHANGES, so nothing was posted.");
        if (body.Length == 0)
            body = verdict == TaskReviewVerdict.Approve ? "Looks good." : "No written findings.";
        // An in-process reviewer submits through its own tool; a coding agent's verdict is its reply.
        if (!progress.Satisfied(workspaceId, RoleTerminationKind.ReviewSubmitted))
        {
            var submitted = await forge.SubmitReviewAsync(
                workspaceId,
                new ForgeReviewRequest(task.Proposal!.Number, body, ReviewVerdictText.Event(verdict)),
                cancellationToken);
            if (!submitted.Submitted)
                return TaskTurnOutcome.Blocked("review_failed", submitted.Refusal ?? "The host did not accept the review.");
        }

        queue.Review(task.Owner, task.Repository, task.Id, new TaskReview(workspaceId, verdict, task.Proposal!.Revision, body), Reviewers(task));
        progress.Record(workspaceId, RoleTerminationKind.ReviewSubmitted);
        return TaskTurnOutcome.Done();
    }

    private async Task<string?> CheckoutAsync(string workspaceId, string branch, IReadOnlyList<string> fetch, CancellationToken cancellationToken)
    {
        var fetched = await forge.FetchBranchesAsync(workspaceId, [.. fetch.Distinct(StringComparer.Ordinal)], cancellationToken);
        if (!fetched.Succeeded)
            return fetched.Error;
        var reset = await git.ResetToRemoteBranchAsync(workspaceId, branch, cancellationToken);
        return reset.Succeeded ? null : reset.Error ?? "The checkout failed.";
    }

    /// <summary>Every reviewer on the task's repository; approval needs each of them.</summary>
    private IReadOnlyCollection<string> Reviewers(QueuedTask task) =>
    [
        .. workspaces.GetAll()
            .Where(workspace => TeamRoles.Reviews(settings.Get(workspace.Id).Member))
            .Where(workspace => forge.ResolveRepository(workspace.Id) is { } repository
                && string.Equals(repository.Owner, task.Owner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(repository.Repository, task.Repository, StringComparison.OrdinalIgnoreCase))
            .Select(workspace => workspace.Id)
    ];

    private QueuedTask? Find(string workspaceId, string id) =>
        forge.ResolveRepository(workspaceId) is { } repository ? queue.Get(repository.Owner, repository.Repository, id) : null;

    private string? PreviousBase(string workspaceId) =>
        queue.List().LastOrDefault(task => string.Equals(task.HeldByWorkspaceId, workspaceId, StringComparison.Ordinal) && task.BaseBranch is not null)?.BaseBranch;

    /// <summary>Commits are the member's: named after its workspace, at an address that cannot receive mail.</summary>
    private GitIdentity Identity(string workspaceId)
    {
        var name = workspaces.GetById(workspaceId)?.DisplayName;
        return new GitIdentity(string.IsNullOrWhiteSpace(name) ? workspaceId : name, $"{workspaceId}@agent-up.invalid");
    }
}
