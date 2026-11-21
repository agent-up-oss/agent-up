using System.Net.Http;
using Microsoft.Extensions.Logging;
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
    TaskReviewRequirementService requirements,
    IReviewApplicationHost? applications = null,
    ILogger<TaskPipelineService>? logger = null) : ITaskPipeline
{
    public async Task<TaskTurnPlan> PrepareAsync(string workspaceId, AgentRoleDefinition role, AssignedIssue issue, CancellationToken cancellationToken)
    {
        var task = Find(workspaceId, issue.Id);
        if (task is null)
            return TaskTurnPlan.Refused("The task is no longer in the queue.");
        try
        {
            return role.Trigger == RoleTriggerKind.ProposalAwaitingReview
                ? await PrepareReviewAsync(workspaceId, role, task, cancellationToken)
                : await PrepareAuthorAsync(workspaceId, role, task, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return TaskTurnPlan.Refused($"The host could not be reached: {exception.Message}");
        }
    }

    public async Task<TaskTurnOutcome> FinishAsync(string workspaceId, AgentRoleDefinition role, AssignedIssue issue, LoopRunResult result, CancellationToken cancellationToken)
    {
        var task = Find(workspaceId, issue.Id);
        if (task is null)
            return TaskTurnOutcome.Blocked("task_missing", "The task left the queue while it was being worked.");
        try
        {
            // A member that already submitted through its task tool is done: the turn's reply is
            // only the fallback for an agent that ends its turn instead.
            if (progress.Satisfied(workspaceId, role.Termination))
                return TaskTurnOutcome.Done();
            return role.Trigger == RoleTriggerKind.ProposalAwaitingReview
                ? await SubmitReviewAsync(workspaceId, task, result, cancellationToken)
                : await PublishWorkAsync(workspaceId, task, result.Reply, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return TaskTurnOutcome.Blocked("host_unavailable", exception.Message);
        }
    }

    /// <summary>
    /// A fresh task starts from its base branch: the Server starts the task branch there, pushes
    /// it so the work is visible on the host from the start, and assigns the issue to the author's
    /// own account, which is how the host learns who took it. One sent back continues on its task
    /// branch, where the earlier revision already is, and its prompt carries what the host said.
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
            var branch = TaskBranch(task);
            var started = await git.StartBranchAsync(workspaceId, branch, cancellationToken);
            if (!started.Succeeded)
                return TaskTurnPlan.Refused(started.Error ?? $"The task branch {branch} could not be started.");
            var pushed = await forge.PushTaskBranchAsync(workspaceId, branch, cancellationToken);
            if (!pushed.Succeeded)
                return TaskTurnPlan.Refused(pushed.Error ?? $"The task branch {branch} could not be pushed.");
            if (await forge.ReadAccountAsync(workspaceId, cancellationToken) is { } account)
                await forge.AssignIssueAsync(workspaceId, task.Number, account.Login, cancellationToken);
            queue.Begin(workspaceId, task.Owner, task.Repository, task.Id, baseBranch);
            task = Find(workspaceId, task.Id)!;
        }

        if (task.Stage == TaskStage.ChangesRequested && task.Proposal is not null)
        {
            var feedback = await forge.ReadFeedbackAsync(workspaceId, task.Proposal.Branch, cancellationToken);
            var account = await forge.ReadAccountAsync(workspaceId, cancellationToken);
            return TaskTurnPlan.Ready(TaskPrompts.Author(role, task, feedback, account?.Login));
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
        await RequestReviewAsync(workspaceId, task, cancellationToken);
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

    /// <summary>
    /// Names this reviewer on the proposal, so the host shows who the review is waiting on. The
    /// author opened the proposal, so the request goes out with the author's credential; it is
    /// repeated for every revision, because a push is a new request for the same reviewer. A host
    /// that refuses only loses that marker, never the review.
    /// </summary>
    private async Task RequestReviewAsync(string workspaceId, QueuedTask task, CancellationToken cancellationToken)
    {
        if (task.HeldByWorkspaceId is not { } author || await forge.ReadAccountAsync(workspaceId, cancellationToken) is not { } reviewer)
            return;
        try
        {
            await forge.RequestReviewAsync(author, task.Proposal!.Number, reviewer.Login, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // The proposal number and workspace come from the host and the request, so they stay out of the log line.
            logger?.LogWarning(exception, "The host did not record a review request.");
        }
    }

    /// <summary>
    /// Turns an author's working tree into the next revision: commit to the task branch under the
    /// member's name, push with its credential, and open the proposal or comment on it with the
    /// author's summary. The same path serves <c>submit_work</c> and an author that just stops.
    /// </summary>
    public async Task<TaskTurnOutcome> PublishWorkAsync(string workspaceId, QueuedTask task, string? summary, CancellationToken cancellationToken)
    {
        var branch = task.Proposal?.Branch ?? TaskBranch(task);
        var baseBranch = task.Proposal?.BaseBranch ?? task.BaseBranch ?? "main";
        var committed = await git.CommitAllToBranchAsync(workspaceId, branch, TaskPrompts.Commit(task, summary), Identity(workspaceId), cancellationToken);
        // A commit already on the task branch from an attempt whose push failed is still the
        // work to publish; only a branch with no proposal yet can be in that state.
        if (committed.NothingToCommit && (task.Proposal is not null || !await AheadOfBaseAsync(workspaceId, branch, baseBranch, cancellationToken)))
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
                new ForgeOpenProposalRequest(task.Title, TaskPrompts.ProposalBody(task, summary), branch, baseBranch),
                cancellationToken);
            if (opened is null)
                return TaskTurnOutcome.Blocked("proposal_failed", "The host did not open the proposal.");
            (number, url) = (opened.Number, opened.Url);
            // The proposal is the author's work as much as the issue is its task.
            if (int.TryParse(number, out var proposal) && await forge.ReadAccountAsync(workspaceId, cancellationToken) is { } author)
                await forge.AssignIssueAsync(workspaceId, proposal, author.Login, cancellationToken);
        }
        else
        {
            (number, url) = (task.Proposal.Number, task.Proposal.Url);
            if (!string.IsNullOrWhiteSpace(summary))
                await forge.CommentAsync(workspaceId, new ForgeCommentRequest(number, summary.Trim()), cancellationToken);
        }

        queue.Propose(workspaceId, task.Owner, task.Repository, task.Id, number, url, branch, baseBranch, head);
        progress.Record(workspaceId, RoleTerminationKind.ProposalOpened);
        return TaskTurnOutcome.Done();
    }

    /// <summary>A coding agent that ends its turn instead of calling submit_review gives its verdict as its reply.</summary>
    private async Task<TaskTurnOutcome> SubmitReviewAsync(string workspaceId, QueuedTask task, LoopRunResult result, CancellationToken cancellationToken)
    {
        var (verdict, body) = ReviewVerdictText.Read(result.Reply);
        // A reply with no verdict is not a review: posting it would put words on the proposal under
        // the reviewer's name that decide nothing, and it would take the task off their list.
        if (verdict == TaskReviewVerdict.Comment)
            return TaskTurnOutcome.Blocked("no_verdict", "The review did not end with VERDICT: APPROVE or VERDICT: REQUEST_CHANGES, so nothing was posted.");
        return await SubmitVerdictAsync(workspaceId, task, verdict, body, [], cancellationToken);
    }

    /// <summary>
    /// Posts one reviewer's verdict on the open revision under its own account, with each
    /// finding on its line, and records it on the task. The review is then in, so the revision
    /// has nothing more to show: the reviewer's workspace stops and it is free for the next one.
    /// </summary>
    public async Task<TaskTurnOutcome> SubmitVerdictAsync(
        string workspaceId,
        QueuedTask task,
        TaskReviewVerdict verdict,
        string body,
        IReadOnlyList<ForgeReviewFindingDto> findings,
        CancellationToken cancellationToken)
    {
        if (body.Trim().Length == 0)
            body = verdict == TaskReviewVerdict.Approve ? "Looks good." : "No written findings.";
        var submitted = await forge.SubmitReviewAsync(
            workspaceId,
            new ForgeReviewRequest(task.Proposal!.Number, body, ReviewVerdictText.Event(verdict), findings, task.Proposal.Head),
            cancellationToken);
        if (!submitted.Submitted)
            return TaskTurnOutcome.Blocked("review_failed", submitted.Refusal ?? "The host did not accept the review.");

        var kind = settings.Get(workspaceId).Member;
        queue.Review(task.Owner, task.Repository, task.Id, new TaskReview(workspaceId, verdict, task.Proposal.Revision, body, kind), requirements.For(task.Owner, task.Repository));
        progress.Record(workspaceId, RoleTerminationKind.ReviewSubmitted);
        if (applications is not null)
            await applications.StopAsync(workspaceId, cancellationToken);
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

    /// <summary>Whether the task branch carries a commit the base does not, from an attempt whose push failed.</summary>
    private async Task<bool> AheadOfBaseAsync(string workspaceId, string branch, string baseBranch, CancellationToken cancellationToken)
    {
        if ((await gitRead.GetHeadAsync(workspaceId, cancellationToken))?.Branch != branch)
            return false;
        var head = await gitRead.ResolveCommitAsync(workspaceId, "HEAD", cancellationToken);
        var start = await gitRead.ResolveCommitAsync(workspaceId, $"refs/remotes/origin/{baseBranch}", cancellationToken);
        return head is not null && start is not null && !string.Equals(head, start, StringComparison.Ordinal);
    }

    private static string TaskBranch(QueuedTask task) => $"{ForgeController.TaskBranchPrefix}issue-{task.Number}";

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
