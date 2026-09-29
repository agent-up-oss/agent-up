using AgentUp.Desktop.Features.Git.Models;

namespace AgentUp.Desktop.Features.Git.Providers;

public static class GitConfirmCopyProvider
{
    private static readonly string[] PushFailureForceNeedles =
    [
        "non-fast-forward",
        "failed to push some refs",
        "force-with-lease",
        "stale info",
        "remote rejected",
        "updates were rejected",
        "[rejected]",
        "tip of your current branch is behind",
        "fetch first",
        "incoming"
    ];

    public static GitConfirmCopy Push(string? branch, string? upstream) =>
        new(
            "Push to upstream?",
            $"This pushes the current branch {BranchLabel(branch)} to {UpstreamLabel(upstream)}.",
            "Push");

    public static GitConfirmCopy ForcePush(string? branch, string? upstream) =>
        new(
            "Force-push with lease?",
            $"This force-pushes branch {BranchLabel(branch)} to {UpstreamLabel(upstream)} with --force-with-lease. "
            + "The remote updates only if nobody else has pushed since your last fetch.",
            "Force push",
            Destructive: true);

    public static GitConfirmCopy Discard(IReadOnlyList<string> files) =>
        new(
            "Discard selected files?",
            $"This discards {files.Count} selected file(s):\n\n{string.Join('\n', files)}",
            "Discard",
            Destructive: true);

    public static bool PushFailureOffersForce(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return false;

        var text = error.ToLowerInvariant();
        return PushFailureForceNeedles.Any(needle => text.Contains(needle, StringComparison.Ordinal));
    }

    public static GitConfirmCopy PushRejected(string error, string? branch, string? upstream) =>
        new(
            "Push rejected",
            $"{PushFailureReason(error)} Force push (--force-with-lease) of branch {BranchLabel(branch)} to {UpstreamLabel(upstream)} "
            + "updates the remote only if nobody else has pushed since your last fetch.",
            "Force push (--force-with-lease)",
            Destructive: true);

    public static string PushFailureReason(string error)
    {
        var text = error.ToLowerInvariant();
        if (text.Contains("behind", StringComparison.Ordinal)
            || text.Contains("fetch first", StringComparison.Ordinal)
            || text.Contains("incoming", StringComparison.Ordinal))
        {
            return "The remote has incoming commits your branch does not have.";
        }

        if (text.Contains("force-with-lease", StringComparison.Ordinal)
            || text.Contains("stale info", StringComparison.Ordinal))
        {
            return "The remote moved since your last fetch, so --force-with-lease could not update it.";
        }

        if (text.Contains("non-fast-forward", StringComparison.Ordinal)
            || text.Contains("rejected", StringComparison.Ordinal)
            || text.Contains("failed to push some refs", StringComparison.Ordinal))
        {
            return "The remote rejected a non-fast-forward update because it has commits you do not.";
        }

        return error.Trim();
    }

    private static string BranchLabel(string? branch)
    {
        var value = branch?.Trim();
        return string.IsNullOrEmpty(value) ? "the current branch" : value;
    }

    private static string UpstreamLabel(string? upstream)
    {
        var value = upstream?.Trim();
        return string.IsNullOrEmpty(value) ? "its upstream" : value;
    }
}
