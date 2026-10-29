using AgentUp.Server.Features.Training.Interfaces;
using AgentUp.Server.Features.Training.Models;
using AgentUp.Server.Features.Training.Services;

namespace AgentUp.Server.Tests.Features.Training.Unit;

/// <summary>
/// The backlog itself: what gets written down, what is dropped as a repeat, and what happens when
/// a product owner finally looks at it.
/// </summary>
[TestFixture]
public sealed class ProductDecisionServiceTests
{
    [Test]
    public void ARecordedDecision_startsWaitingForAVerdict()
    {
        var service = new ProductDecisionService(new MemoryDecisionStore());

        var recording = Record(service);

        Assert.Multiple(() =>
        {
            Assert.That(recording.Recorded, Is.True);
            Assert.That(recording.Decision!.Verdict, Is.EqualTo(DecisionVerdict.Pending));
            Assert.That(recording.Decision.Reviewed, Is.False);
            Assert.That(recording.Decision.ReviewedAt, Is.Null);
            Assert.That(recording.Decision.RepositoryKey, Is.EqualTo("acme/widgets"));
        });
    }

    [Test]
    public void AMalformedDraft_isNotWrittenDownAtAll()
    {
        var store = new MemoryDecisionStore();
        var service = new ProductDecisionService(store);

        var recording = Record(service, headline: "no");

        Assert.Multiple(() =>
        {
            Assert.That(recording.Recorded, Is.False);
            Assert.That(recording.Faults, Is.Not.Empty);
            Assert.That(service.All(), Is.Empty);
            Assert.That(store.Writes, Is.Zero, "nothing was persisted for a draft that was refused");
        });
    }

    [Test]
    public void TheSameDecisionRecordedTwice_isDroppedRatherThanQueuedAgain()
    {
        // Agents work in loops and reach the same conclusion more than once. The person reviewing
        // should not have to dismiss it twice.
        var service = new ProductDecisionService(new MemoryDecisionStore());
        var first = Record(service);

        var second = Record(service, detail: "Said again in different words, but the same call.");

        Assert.Multiple(() =>
        {
            Assert.That(second.Recorded, Is.False);
            Assert.That(second.Duplicate!.Id, Is.EqualTo(first.Decision!.Id));
            Assert.That(service.All(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AVerdict_isKeptWithTheDecisionItJudged()
    {
        var service = new ProductDecisionService(new MemoryDecisionStore());
        var recorded = Record(service).Decision!;

        var reviewed = service.Review(recorded.Id, DecisionVerdict.Good, "  Agreed, keep it.  ", At(5));

        Assert.Multiple(() =>
        {
            Assert.That(reviewed!.Verdict, Is.EqualTo(DecisionVerdict.Good));
            Assert.That(reviewed.Note, Is.EqualTo("Agreed, keep it."));
            Assert.That(reviewed.ReviewedAt, Is.EqualTo(At(5)));
            Assert.That(reviewed.Reviewed, Is.True);
        });
    }

    [Test]
    public void AProductOwnerMayChangeTheirMind()
    {
        var service = new ProductDecisionService(new MemoryDecisionStore());
        var recorded = Record(service).Decision!;
        service.Review(recorded.Id, DecisionVerdict.Good, "Fine", At(5));

        var again = service.Review(recorded.Id, DecisionVerdict.Bad, null, At(6));

        Assert.Multiple(() =>
        {
            Assert.That(again!.Verdict, Is.EqualTo(DecisionVerdict.Bad));
            Assert.That(again.Note, Is.Null, "the note belonged to the verdict that was replaced");
            Assert.That(service.All(), Has.Count.EqualTo(1), "judging again does not add a second record");
        });
    }

    [Test]
    public void TakingTheVerdictBackAgain_leavesNothingToExport()
    {
        var service = new ProductDecisionService(new MemoryDecisionStore());
        var recorded = Record(service).Decision!;
        service.Review(recorded.Id, DecisionVerdict.Good, "Fine", At(5));

        var undone = service.Review(recorded.Id, DecisionVerdict.Pending, null, At(6));

        Assert.Multiple(() =>
        {
            Assert.That(undone!.Reviewed, Is.False);
            Assert.That(undone.ReviewedAt, Is.Null);
        });
    }

    [Test]
    public void JudgingSomethingThatIsNotThere_changesNothing()
    {
        var service = new ProductDecisionService(new MemoryDecisionStore());

        Assert.That(service.Review("nope", DecisionVerdict.Good, null, At(1)), Is.Null);
    }

    [Test]
    public void TheUnreviewedComeFirst_becauseTheyAreTheReasonToOpenTheTab()
    {
        var service = new ProductDecisionService(new MemoryDecisionStore());
        var first = Record(service, headline: "Queue the work on the Server rather than the host").Decision!;
        Record(service, headline: "Drop the second confirmation step from checkout", at: At(2));
        service.Review(first.Id, DecisionVerdict.Good, null, At(5));

        var listed = service.List();

        Assert.That(
            listed.Select(decision => decision.Reviewed),
            Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public void AReviewerCanAskForOnlyWhatIsStillWaiting()
    {
        var service = new ProductDecisionService(new MemoryDecisionStore());
        var first = Record(service).Decision!;
        Record(service, headline: "Drop the second confirmation step from checkout", at: At(2));
        service.Review(first.Id, DecisionVerdict.Bad, null, At(5));

        Assert.That(service.List(includeReviewed: false), Has.Count.EqualTo(1));
    }

    [Test]
    public void TheBacklogNarrowsToOneRepositoryAndCategory()
    {
        var service = new ProductDecisionService(new MemoryDecisionStore());
        Record(service);
        Record(service, category: DecisionCategory.GuiDecisions, headline: "Put the reorder grip on the left of the row");
        Record(service, repository: "other", headline: "Ship the importer before the exporter");

        Assert.Multiple(() =>
        {
            Assert.That(service.List(owner: "acme", repository: "widgets"), Has.Count.EqualTo(2));
            Assert.That(service.List(owner: "ACME", repository: "WIDGETS"), Has.Count.EqualTo(2), "a repository is the same whatever its case");
            Assert.That(service.List(category: DecisionCategory.GuiDecisions), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TheBacklogSurvivesARestart()
    {
        var store = new MemoryDecisionStore();
        var service = new ProductDecisionService(store);
        var recorded = Record(service).Decision!;
        service.Review(recorded.Id, DecisionVerdict.Good, "Keep it", At(5));

        var afterRestart = new ProductDecisionService(store);

        Assert.Multiple(() =>
        {
            Assert.That(afterRestart.All(), Has.Count.EqualTo(1));
            Assert.That(afterRestart.All()[0].Verdict, Is.EqualTo(DecisionVerdict.Good));
            Assert.That(afterRestart.All()[0].Note, Is.EqualTo("Keep it"));
        });
    }

    private static DecisionRecording Record(
        ProductDecisionService service,
        string headline = "Queue the work on the Server rather than the host",
        string detail = "The Server owns the order of work, and the host only reports what is open.",
        string repository = "widgets",
        DecisionCategory category = DecisionCategory.ProductVision,
        DateTimeOffset? at = null)
        => service.Record(
            "acme",
            repository,
            "ws-1",
            "Lucy",
            category,
            headline,
            detail,
            "The host cannot assign an agent, so the order had to live somewhere else.",
            [],
            at ?? At(1));

    private static DateTimeOffset At(int hour) => new(2026, 1, 1, hour, 0, 0, TimeSpan.Zero);

    private sealed class MemoryDecisionStore : IProductDecisionStore
    {
        private IReadOnlyList<ProductDecision> _decisions = [];

        public int Writes { get; private set; }

        public IReadOnlyList<ProductDecision> Read() => _decisions;

        public void Write(IReadOnlyList<ProductDecision> decisions)
        {
            _decisions = [.. decisions];
            Writes++;
        }
    }
}
