using AgentUp.Sdk.Forge;

namespace AgentUp.Server.Features.TaskSource.Services;

/// <summary>One kind of review inferred from a pull request.</summary>
public sealed record InferredReview(string Kind, string Standing, string? Login, string? Verdict);

/// <summary>
/// Which reviews a pull request still needs, read from the pull request itself.
///
/// The host only counts approvals. The kinds that matter are the kinds of the people it asked to
/// review and the kinds of the people who already reviewed. A kind nobody on the team maps to
/// falls back to every reviewer kind the team has on the repository, so a pull request with no
/// requested reviewers still shows the reviews the team owes. The latest decisive review wins;
/// a comment does not settle a kind.
/// </summary>
public static class PullRequestReviewInference
{
    public static IReadOnlyList<InferredReview> For(
        ForgePullRequest pull,
        IReadOnlyDictionary<string, string> kindByLogin,
        IReadOnlyCollection<string> teamKinds)
    {
        var mentioned = pull.RequestedReviewers
            .Concat(pull.Reviews.Select(review => review.Login))
            .Where(kindByLogin.ContainsKey)
            .Select(login => kindByLogin[login])
            .ToHashSet(StringComparer.Ordinal);

        var required = mentioned.Count > 0
            ? mentioned
            : teamKinds.ToHashSet(StringComparer.Ordinal);

        return required
            .OrderBy(Rank)
            .ThenBy(kind => kind, StringComparer.Ordinal)
            .Select(kind => Standing(pull, kind, kindByLogin))
            .ToArray();
    }

    private static InferredReview Standing(
        ForgePullRequest pull,
        string kind,
        IReadOnlyDictionary<string, string> kindByLogin)
    {
        var decisive = pull.Reviews
            .Where(review => kindByLogin.TryGetValue(review.Login, out var mapped) && mapped == kind)
            .OrderBy(review => review.SubmittedAt)
            .LastOrDefault(review => IsDecisive(review.State));
        if (decisive is not null)
        {
            var approved = decisive.State.Contains("APPROV", StringComparison.OrdinalIgnoreCase);
            return new InferredReview(kind, "reviewed", decisive.Login, approved ? "Approved" : "Changes requested");
        }

        var requested = pull.RequestedReviewers.FirstOrDefault(login =>
            kindByLogin.TryGetValue(login, out var mapped) && mapped == kind);
        return requested is not null
            ? new InferredReview(kind, "reviewing", requested, null)
            : new InferredReview(kind, "waiting", null, null);
    }

    private static bool IsDecisive(string state) =>
        state.Contains("APPROV", StringComparison.OrdinalIgnoreCase)
        || state.Contains("CHANGE", StringComparison.OrdinalIgnoreCase);

    private static int Rank(string kind) => kind switch
    {
        "devReviewer" => 0,
        "pmReviewer" => 1,
        _ => 2,
    };
}
