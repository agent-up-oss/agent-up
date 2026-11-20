using System.Text;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>What a team member is told about the task in front of it, built from the queue's record.</summary>
public static class TaskPrompts
{
    /// <summary>The issue leads, so even a small model reads what to change before how to behave.</summary>
    public static string Author(AgentRoleDefinition role, QueuedTask task)
    {
        var prompt = new StringBuilder()
            .AppendLine($"Issue #{task.Number}: {task.Title}")
            .AppendLine()
            .AppendLine(task.Body.Trim());
        var requested = task.CurrentReviews.Where(review => review.Verdict == TaskReviewVerdict.RequestChanges).ToArray();
        if (task.Stage == TaskStage.ChangesRequested && requested.Length > 0)
        {
            prompt.AppendLine()
                .AppendLine($"Your earlier change for this issue (proposal {task.Proposal!.Url}, revision {task.Proposal.Revision}) was reviewed and changes were requested. It is already in the working tree on {task.Proposal.Branch}. Address every point below.");
            foreach (var review in requested)
                prompt.AppendLine().AppendLine("Review:").AppendLine(review.Body.Trim());
        }

        return prompt.AppendLine().AppendLine(role.Prompt).ToString().TrimEnd();
    }

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
