using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.TaskSource.Services;

namespace AgentUp.Server.Tests.Features.TaskSource.Unit;

[TestFixture]
public sealed class PullRequestReviewInferenceTests
{
    private static readonly IReadOnlyDictionary<string, string> Team = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["hector"] = "devReviewer",
        ["lucy"] = "pmReviewer",
    };

    [Test]
    public void ARequestedReviewerWhoHasNotAnsweredIsUnderReview()
    {
        var reviews = PullRequestReviewInference.For(Pull(["hector"], []), Team, ["devReviewer", "pmReviewer"]);

        Assert.Multiple(() =>
        {
            Assert.That(reviews.Single().Kind, Is.EqualTo("devReviewer"));
            Assert.That(reviews.Single().Standing, Is.EqualTo("reviewing"));
            Assert.That(reviews.Single().Login, Is.EqualTo("hector"));
        });
    }

    [Test]
    public void TheLatestDecisiveReviewSettlesTheKindAndACommentDoesNot()
    {
        var reviews = PullRequestReviewInference.For(
            Pull([],
            [
                new ForgeReview("hector", "CHANGES_REQUESTED", DateTimeOffset.UnixEpoch),
                new ForgeReview("hector", "APPROVED", DateTimeOffset.UnixEpoch.AddHours(1)),
                new ForgeReview("hector", "COMMENTED", DateTimeOffset.UnixEpoch.AddHours(2)),
            ]),
            Team,
            ["devReviewer"]);

        Assert.Multiple(() =>
        {
            Assert.That(reviews.Single().Standing, Is.EqualTo("reviewed"));
            Assert.That(reviews.Single().Verdict, Is.EqualTo("Approved"));
        });
    }

    [Test]
    public void AnUnknownKindSortsAfterDevAndPmReview()
    {
        var reviews = PullRequestReviewInference.For(Pull([], []), Team, ["other", "pmReviewer", "devReviewer"]);

        Assert.That(reviews.Select(review => review.Kind), Is.EqualTo(new[] { "devReviewer", "pmReviewer", "other" }));
    }

    [Test]
    public void APullWithNobodyTheTeamKnowsOwesEveryReviewerKindTheTeamHas()
    {
        var reviews = PullRequestReviewInference.For(Pull([], []), Team, ["pmReviewer", "devReviewer"]);

        Assert.That(reviews.Select(review => $"{review.Kind}:{review.Standing}"), Is.EqualTo(new[]
        {
            "devReviewer:waiting",
            "pmReviewer:waiting",
        }));
    }

    [Test]
    public void ADecisiveReviewOnTheCurrentHeadSettlesTheKind()
    {
        var reviews = PullRequestReviewInference.For(
            Pull([], [new ForgeReview("hector", "APPROVED", DateTimeOffset.UnixEpoch, "aaa")], "aaa"),
            Team,
            ["devReviewer"]);

        Assert.Multiple(() =>
        {
            Assert.That(reviews.Single().Standing, Is.EqualTo("reviewed"));
            Assert.That(reviews.Single().CommitId, Is.EqualTo("aaa"));
        });
    }

    [Test]
    public void ADecisiveReviewOnAnEarlierShaLeavesTheKindWaiting()
    {
        var reviews = PullRequestReviewInference.For(
            Pull([], [new ForgeReview("hector", "APPROVED", DateTimeOffset.UnixEpoch, "old")], "aaa"),
            Team,
            ["devReviewer"]);

        Assert.That(reviews.Single().Standing, Is.EqualTo("waiting"));
    }

    [Test]
    public void ADecisiveReviewWithoutACommitDoesNotCoverANamedHead()
    {
        var reviews = PullRequestReviewInference.For(
            Pull([], [new ForgeReview("hector", "APPROVED", DateTimeOffset.UnixEpoch)], "aaa"),
            Team,
            ["devReviewer"]);

        Assert.That(reviews.Single().Standing, Is.EqualTo("waiting"));
    }

    private static ForgePullRequest Pull(
        IReadOnlyList<string> requested,
        IReadOnlyList<ForgeReview> reviews,
        string? headSha = null) =>
        new("42", 42, "Product cards", "https://example.test/42", requested, reviews, HeadSha: headSha);
}
