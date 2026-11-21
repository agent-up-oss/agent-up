using System.Net.Http;
using System.Text;
using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.DTOs;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// The task tools an agent has. An agent never commits, pushes, opens a proposal, or writes on
/// the host: it reads the task in front of it, and says it is done - an author with its work, a
/// reviewer with its verdict and findings - and the Server does the rest on the member's behalf
/// with the member's own credential.
/// </summary>
public sealed class TaskWorkService(
    TaskQueueService queue,
    TaskPipelineService pipeline,
    ForgeController forge,
    IAgentWorkspaceSettingsRepository settings,
    IReviewEvidenceSource? evidence = null)
{
    private const int ScreenshotLimit = 6;

    /// <summary>The task this member is on, or null when it holds none.</summary>
    public async Task<TaskBriefDto?> DescribeAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var kind = settings.Get(workspaceId).Member;
        var task = Current(workspaceId, kind);
        if (task is null)
            return null;

        ForgeTaskFeedbackDto? feedback = null;
        var branch = task.Proposal?.Branch;
        if (branch is not null)
        {
            try
            {
                feedback = await forge.ReadFeedbackAsync(workspaceId, branch, cancellationToken);
            }
            catch (HttpRequestException)
            {
                feedback = null;
            }
        }

        var threads = (feedback?.Threads ?? []).Where(thread => !thread.IsResolved && thread.Summary.Trim().Length > 0).ToArray();
        return new TaskBriefDto(
            Name(kind),
            task.Number,
            task.Title,
            task.Body,
            task.Url,
            Name(task.Stage),
            task.Proposal is null
                ? null
                : new TaskProposalDto(task.Proposal.Number, task.Proposal.Url, task.Proposal.Branch, task.Proposal.BaseBranch, task.Proposal.Revision, task.Proposal.Head),
            [.. task.CurrentReviews.Select(review => new TaskReviewDto(review.WorkspaceId, Name(review.Verdict), review.Revision, review.Body, Name(review.Kind)))],
            [.. threads.Where(thread => thread.Path is not null).Select(thread => new TaskFindingDto(thread.Path!, thread.Line ?? 0, thread.Summary.Trim(), thread.Author))],
            [.. (feedback?.Checks ?? []).GroupBy(check => check.Name, StringComparer.Ordinal).Select(group => group.First())
                .Select(check => new TaskCheckDto(check.Name, check.Status, check.Conclusion, check.Url))],
            [.. threads.Where(thread => thread.Path is null).Select(thread => new TaskCommentDto(thread.Author, thread.Summary.Trim()))],
            Next(kind, task));
    }

    /// <summary>
    /// An author is done changing files: the Server commits, pushes and opens or updates the
    /// proposal. Only an author holding a task that is being worked can submit.
    /// </summary>
    public async Task<TaskSubmissionDto> SubmitWorkAsync(string workspaceId, string? summary, CancellationToken cancellationToken)
    {
        if (settings.Get(workspaceId).Member != TeamMemberKind.Author)
            return Refused("Only an author submits work; reviewers submit a review.");
        var task = queue.Held(workspaceId);
        if (task is null || task.Stage is not (TaskStage.Implementing or TaskStage.ChangesRequested))
            return Refused("You hold no task that is being worked on, so there is nothing to submit.");

        try
        {
            var outcome = await pipeline.PublishWorkAsync(workspaceId, task, summary, cancellationToken);
            if (!outcome.Published)
                return Refused(outcome.Cause ?? "The work could not be published.");
        }
        catch (HttpRequestException exception)
        {
            return Refused($"The host could not be reached: {exception.Message}");
        }

        var proposal = queue.Get(task.Owner, task.Repository, task.Id)?.Proposal;
        return new TaskSubmissionDto(true, $"Revision {proposal?.Revision} of {proposal?.Url} is up for review. You are done with this task until the reviewers answer.", proposal?.Url);
    }

    /// <summary>
    /// A reviewer's verdict on the revision it holds: posted under its account with each finding
    /// on its line and each screenshot attached, then recorded on the task. Submitting stops the
    /// reviewer's workspace and frees it for the next review.
    /// </summary>
    public async Task<TaskSubmissionDto> SubmitReviewAsync(
        string workspaceId,
        string verdict,
        string summary,
        IReadOnlyList<TaskFindingDto>? findings,
        IReadOnlyList<string>? screenshotArtifactIds,
        CancellationToken cancellationToken)
    {
        var kind = settings.Get(workspaceId).Member;
        if (!TeamRoles.Reviews(kind))
            return Refused("Only a reviewer submits a review; an author submits work.");
        var decided = verdict.Trim().ToLowerInvariant() switch
        {
            "approve" or "approved" => TaskReviewVerdict.Approve,
            "request_changes" or "requestchanges" or "changes_requested" => TaskReviewVerdict.RequestChanges,
            _ => (TaskReviewVerdict?)null
        };
        if (decided is null)
            return Refused("verdict must be approve or request_changes.");
        var task = Current(workspaceId, kind);
        if (task?.Proposal is null)
            return Refused("You hold no review, so there is nothing to submit.");
        var shots = screenshotArtifactIds ?? [];
        if (shots.Count > ScreenshotLimit)
            return Refused($"Attach at most {ScreenshotLimit} screenshots.");
        var invalid = (findings ?? []).Where(finding => string.IsNullOrWhiteSpace(finding.Path) || finding.Line < 1 || string.IsNullOrWhiteSpace(finding.Comment)).ToArray();
        if (invalid.Length > 0)
            return Refused("Each finding needs a path, a line from 1, and a comment.");

        try
        {
            var body = await WithScreenshotsAsync(workspaceId, task.Proposal.Number, summary, shots, cancellationToken);
            if (body.Missing.Count > 0)
                return Refused($"No screenshot was found for {string.Join(", ", body.Missing)}; pass the artifact id browser_screenshot returned.");
            var outcome = await pipeline.SubmitVerdictAsync(
                workspaceId,
                task,
                decided.Value,
                body.Text,
                [.. (findings ?? []).Select(finding => new ForgeReviewFindingDto(finding.Path.Trim(), finding.Line, finding.Comment.Trim()))],
                cancellationToken);
            if (!outcome.Published)
                return Refused(outcome.Cause ?? "The review was not accepted.");
        }
        catch (HttpRequestException exception)
        {
            return Refused($"The host could not be reached: {exception.Message}");
        }

        return new TaskSubmissionDto(true, $"Your review of revision {task.Proposal.Revision} is on {task.Proposal.Url}. The review is done and your workspace was stopped.", task.Proposal.Url);
    }

    private async Task<(string Text, IReadOnlyList<string> Missing)> WithScreenshotsAsync(
        string workspaceId,
        string number,
        string summary,
        IReadOnlyList<string> artifactIds,
        CancellationToken cancellationToken)
    {
        var text = new StringBuilder(summary.Trim());
        var missing = new List<string>();
        var index = 0;
        foreach (var artifactId in artifactIds)
        {
            index++;
            var capture = evidence is null ? null : await evidence.ReadAsync(artifactId, cancellationToken);
            if (capture is null)
            {
                missing.Add(artifactId);
                continue;
            }

            var name = $"screenshot-{index}{(capture.Value.MimeType == "image/jpeg" ? ".jpg" : ".png")}";
            var url = await forge.AttachAsync(workspaceId, number, name, capture.Value.MimeType, capture.Value.Bytes, cancellationToken);
            text.AppendLine().AppendLine().Append(url is null ? $"(Screenshot {index} could not be attached on this host.)" : $"![Screenshot {index}]({url})");
        }

        return (text.ToString(), missing);
    }

    /// <summary>
    /// What this member is on: an author's held task; a reviewer's is the task its loop is
    /// reviewing, else the one whose review of its kind it holds and has not given yet.
    /// </summary>
    private QueuedTask? Current(string workspaceId, TeamMemberKind kind)
    {
        if (!TeamRoles.Reviews(kind))
            return queue.Held(workspaceId);

        var reviewing = queue.List().Where(task =>
            task.Proposal is not null
            && task.Stage is TaskStage.InReview or TaskStage.ChangesRequested
            && string.Equals(task.ReviewerFor(kind), workspaceId, StringComparison.Ordinal)
            && task.CurrentReviews.All(review => !string.Equals(review.WorkspaceId, workspaceId, StringComparison.Ordinal)))
            .ToArray();
        var looping = settings.Get(workspaceId).Loop?.CurrentTask?.Id;
        return reviewing.FirstOrDefault(task => task.Id == looping) ?? reviewing.FirstOrDefault();
    }

    private static string Next(TeamMemberKind kind, QueuedTask task) => kind switch
    {
        TeamMemberKind.DevReviewer =>
            $"Read the change with git diff origin/{task.Proposal?.BaseBranch ?? "main"}...HEAD and check it against the issue. Start the workspace with start_workspace to see that it runs. "
            + "Then call submit_review once with verdict approve or request_changes, a summary, and a finding for each problem at its path and line. You may not change any file.",
        TeamMemberKind.PmReviewer =>
            "The revision is running and open in your browser. Use it the way the issue describes, as a user would, without reading code. "
            + "Take a browser_screenshot of anything that is wrong or confusing. Then call submit_review once with verdict approve or request_changes, "
            + "a summary of what you tried and saw, and the screenshot artifact ids. If you cannot do what the issue asks through the interface, request changes.",
        _ when task.Stage == TaskStage.ChangesRequested =>
            "Address every review, finding, failing check and comment, then call submit_work with a summary of what you changed. Do not commit or push yourself.",
        _ => "Change the files the issue needs, then call submit_work with a summary of the change. Do not commit or push yourself."
    };

    private static TaskSubmissionDto Refused(string message) => new(false, message);

    private static string Name<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(Enum.GetName(value)!);
}
