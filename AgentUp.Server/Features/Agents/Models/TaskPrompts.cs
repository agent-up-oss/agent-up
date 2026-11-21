using System.Text;
using AgentUp.Server.Features.Forge.DTOs;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>What a team member is told about the task in front of it, built from the queue's record.</summary>
public static class TaskPrompts
{
    /// <summary>
    /// The issue leads, so even a small model reads what to change before how to behave. A task
    /// sent back carries everything the host said about the revision: each finding at its file and
    /// line, every failing check with where to read its log, the comments, and the reviews.
    /// </summary>
    public static string Author(AgentRoleDefinition role, QueuedTask task, ForgeTaskFeedbackDto? feedback = null, string? authorLogin = null)
    {
        var prompt = new StringBuilder()
            .AppendLine($"Issue #{task.Number}: {task.Title}")
            .AppendLine()
            .AppendLine(task.Body.Trim());
        if (task.Stage == TaskStage.ChangesRequested && task.Proposal is not null)
        {
            prompt.AppendLine()
                .AppendLine($"Your earlier change for this issue (proposal {task.Proposal.Url}, revision {task.Proposal.Revision}) needs more work. It is already in the working tree on {task.Proposal.Branch}. Address every point below.");
            foreach (var review in task.CurrentReviews.Where(review => review.Verdict == TaskReviewVerdict.RequestChanges))
                prompt.AppendLine().AppendLine($"Review ({Kind(review.Kind)}):").AppendLine(review.Body.Trim());
            AppendFeedback(prompt, feedback, authorLogin);
        }

        return prompt.AppendLine().AppendLine(role.Prompt).ToString().TrimEnd();
    }

    private static void AppendFeedback(StringBuilder prompt, ForgeTaskFeedbackDto? feedback, string? authorLogin)
    {
        if (feedback is null)
            return;
        var failing = TaskChecks.Failing(feedback.Checks).ToArray();
        if (failing.Length > 0)
        {
            prompt.AppendLine().AppendLine("Failing checks (read the log at the link):");
            foreach (var check in failing)
                prompt.AppendLine($"- {check.Name}: {check.Conclusion}{(check.Url.Length > 0 ? $" ({check.Url})" : "")}");
        }

        var others = feedback.Threads
            .Where(thread => !thread.IsResolved && thread.Summary.Trim().Length > 0
                && !string.Equals(thread.Author, authorLogin, StringComparison.OrdinalIgnoreCase))
            .TakeLast(FeedbackLimit)
            .ToArray();
        var findings = others.Where(thread => thread.Path is not null).ToArray();
        if (findings.Length > 0)
        {
            prompt.AppendLine().AppendLine("Findings on the code:");
            foreach (var finding in findings)
                prompt.AppendLine($"- {finding.Path}{(finding.Line is { } line ? $":{line}" : "")}{By(finding.Author)}: {finding.Summary.Trim()}");
        }

        var comments = others.Where(thread => thread.Path is null).ToArray();
        if (comments.Length > 0)
        {
            prompt.AppendLine().AppendLine("Comments on the proposal:");
            foreach (var comment in comments)
                prompt.AppendLine($"-{By(comment.Author)} {comment.Summary.Trim()}");
        }
    }

    private static string By(string? author) => string.IsNullOrWhiteSpace(author) ? "" : $" ({author})";

    private static string Kind(TeamMemberKind kind) => kind == TeamMemberKind.PmReviewer ? "product" : "code";

    private const int FeedbackLimit = 30;

    /// <summary>
    /// The change leads, inline, so a reviewer reads exactly the revision under review without
    /// needing a tool to find it; a long diff is cut and says so.
    /// </summary>
    public static string Reviewer(AgentRoleDefinition role, QueuedTask task, string? diff)
    {
        var change = (diff ?? string.Empty).Trim();
        if (change.Length > DiffLimit)
            change = change[..DiffLimit] + $"\n… (diff cut at {DiffLimit} characters; read the rest with git diff origin/{task.Proposal!.BaseBranch}...HEAD)";
        return new StringBuilder()
            .AppendLine($"Review this change for issue #{task.Number}: {task.Title}")
            .AppendLine()
            .AppendLine("The issue asked:")
            .AppendLine(task.Body.Trim())
            .AppendLine()
            .AppendLine($"The change (revision {task.Proposal!.Revision}, branch {task.Proposal.Branch} into {task.Proposal.BaseBranch}):")
            .AppendLine("```diff")
            .AppendLine(change.Length == 0 ? "(no difference from the base branch)" : change)
            .AppendLine("```")
            .AppendLine()
            .AppendLine(role.Prompt)
            .ToString()
            .TrimEnd();
    }

    /// <summary>
    /// What a product reviewer reads: the issue and where the running revision answers, never the
    /// code. The Server started the revision before the turn, so the addresses are that revision.
    /// </summary>
    public static string ProductReviewer(AgentRoleDefinition role, QueuedTask task, IReadOnlyList<string> addresses)
    {
        var prompt = new StringBuilder()
            .AppendLine($"Review the change for issue #{task.Number}: {task.Title}")
            .AppendLine()
            .AppendLine("The issue asked:")
            .AppendLine(task.Body.Trim())
            .AppendLine()
            .AppendLine($"Revision {task.Proposal!.Revision} of {task.Proposal.Url} is running in your workspace.");
        foreach (var address in addresses)
            prompt.AppendLine($"It is open in your browser at {address} - inspect the page to see it.");
        return prompt
            .AppendLine()
            .AppendLine(role.Prompt)
            .ToString()
            .TrimEnd();
    }

    private const int DiffLimit = 12_000;

    /// <summary>The commit message for one revision of an author's work.</summary>
    public static string Commit(QueuedTask task, string? summary)
    {
        var subject = task.Proposal is null
            ? $"{task.Title} (#{task.Number})"
            : $"Address review of #{task.Number} (revision {task.Proposal.Revision + 1})";
        var body = Trim(summary, 2000);
        return body.Length == 0 ? subject : $"{subject}\n\n{body}";
    }

    /// <summary>The proposal description: the issue it closes and the author's own account of the change.</summary>
    public static string ProposalBody(QueuedTask task, string? summary)
    {
        var body = Trim(summary, 4000);
        return body.Length == 0 ? $"Closes #{task.Number}." : $"Closes #{task.Number}.\n\n{body}";
    }

    private static string Trim(string? text, int limit)
    {
        var value = (text ?? string.Empty).Trim();
        return value.Length <= limit ? value : value[..limit].TrimEnd() + "…";
    }
}
