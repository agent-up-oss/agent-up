using AgentUp.Server.Features.Training.Models;
using AgentUp.Server.Features.Training.Providers;

namespace AgentUp.Server.Tests.Features.Training.Provider;

/// <summary>
/// What a decision has to look like before it is worth a product owner's swipe. These rules can
/// only judge shape, so each one is about an entry that is unreadable on its face rather than
/// about whether the decision behind it mattered.
/// </summary>
[TestFixture]
public sealed class DecisionDraftProviderTests
{
    [Test]
    public void AWellFormedDraft_hasNothingWrongWithIt()
    {
        Assert.That(Faults(), Is.Empty);
    }

    [Test]
    public void AHeadlineTooShortToReadOnItsOwn_isRefused()
    {
        Assert.That(Fields(Faults(headline: "Use tabs")), Does.Contain("headline"));
    }

    [Test]
    public void AHeadlineTooLongForACard_isRefused()
    {
        Assert.That(Fields(Faults(headline: new string('a', DecisionDraftProvider.HeadlineMaximum + 1))), Does.Contain("headline"));
    }

    [Test]
    public void AHeadlineAcrossTwoLines_isRefused()
    {
        // The card gives it one line, so the second would simply not be read.
        Assert.That(Fields(Faults(headline: "Queue the work locally\nrather than on the host")), Does.Contain("headline"));
    }

    [TestCase("I decided to queue the work locally rather than on the host")]
    [TestCase("We decided that the queue lives on the Server")]
    [TestCase("Chose to put the queue on the Server rather than the host")]
    public void AHeadlineThatOpensWithoutSayingAnything_isRefused(string headline)
    {
        // A reviewer already knows it is a decision; the line should spend its width on which one.
        Assert.That(Fields(Faults(headline: headline)), Does.Contain("headline"));
    }

    [Test]
    public void ADetailThatOnlyRestatesTheHeadline_isRefused()
    {
        Assert.That(Fields(Faults(detail: "Same as above.")), Does.Contain("detail"));
    }

    [Test]
    public void EvidenceThatAmountsToItSeemedRight_isRefused()
    {
        Assert.That(Fields(Faults(evidence: "Felt right.")), Does.Contain("evidence"));
    }

    [Test]
    public void MorePicturesThanAReviewerWillLookAt_areRefused()
    {
        var images = Enumerable.Range(0, DecisionDraftProvider.EvidenceImageMaximum + 1)
            .Select(index => $"artifact-{index}")
            .ToList();

        Assert.That(Fields(Faults(images: images)), Does.Contain("evidenceArtifactIds"));
    }

    [Test]
    public void AnEmptyArtifactId_isRefusedRatherThanStored()
    {
        Assert.That(Fields(Faults(images: ["artifact-1", "  "])), Does.Contain("evidenceArtifactIds"));
    }

    [Test]
    public void EveryProblemIsReportedAtOnce_soTheAgentFixesItInOneGo()
    {
        var faults = Faults(headline: "no", detail: "no", evidence: "no");

        Assert.That(Fields(faults), Is.EquivalentTo(new[] { "headline", "detail", "evidence" }));
    }

    [Test]
    public void TheSameDecisionRecordedTwice_isRecognisedThroughPunctuationAndCase()
    {
        var existing = Decision("Queue the work on the Server, not the host");

        Assert.Multiple(() =>
        {
            Assert.That(
                DecisionDraftProvider.SameDecision(existing, "acme", "widgets", DecisionCategory.ProductVision,
                    "queue the work on the server — not the host"),
                Is.True);
            Assert.That(
                DecisionDraftProvider.SameDecision(existing, "acme", "widgets", DecisionCategory.GuiDecisions,
                    "Queue the work on the Server, not the host"),
                Is.False,
                "the same words about a different part of the product are a different decision");
            Assert.That(
                DecisionDraftProvider.SameDecision(existing, "acme", "other", DecisionCategory.ProductVision,
                    "Queue the work on the Server, not the host"),
                Is.False,
                "a decision belongs to the repository it was taken in");
        });
    }

    private static ProductDecision Decision(string headline) => new(
        "id", "acme", "widgets", "ws", "Lucy", DecisionCategory.ProductVision,
        headline, "detail", "evidence", [], DateTimeOffset.UnixEpoch);

    private static IReadOnlyList<DecisionDraftFault> Faults(
        string headline = "Queue the work on the Server rather than the host",
        string detail = "The Server owns the order of work, and the host only reports what is open.",
        string evidence = "The host cannot assign an agent, so order had to live somewhere else.",
        IReadOnlyList<string>? images = null)
        => DecisionDraftProvider.Faults(headline, detail, evidence, images);

    private static IEnumerable<string> Fields(IReadOnlyList<DecisionDraftFault> faults)
        => faults.Select(fault => fault.Field).Distinct();
}
