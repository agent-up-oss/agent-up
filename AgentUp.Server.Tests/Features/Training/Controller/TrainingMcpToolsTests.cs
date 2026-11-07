using AgentUp.Server.Features.Training.Controllers;
using AgentUp.Server.Features.Training.DTOs;
using AgentUp.Server.Features.Training.Interfaces;
using AgentUp.Server.Features.Training.Models;
using AgentUp.Server.Features.Training.Providers;
using AgentUp.Server.Features.Training.Services;

namespace AgentUp.Server.Tests.Features.Training.Controller;

/// <summary>
/// The tool an agent calls mid-run to write a decision down.
///
/// Every one of these is about what the agent is told, because the agent is the only thing that
/// reads the reply: it records and carries on, so a refusal has to carry enough for it to fix the
/// entry without asking anything else.
/// </summary>
[TestFixture]
public sealed class TrainingMcpToolsTests
{
    [Test]
    public async Task RecordDecision_writesItDownAgainstTheWorkspacesOwnRepository()
    {
        var tools = Tools(out var decisions);

        var result = await tools.RecordDecision(
            "ws-1",
            "product-vision",
            "Queue the work on the Server rather than the host",
            "The Server owns the order of work; the host only reports what is open.",
            "The host cannot assign an agent, so the order had to live somewhere else.",
            agent: "Lucy");

        var recorded = decisions.All().Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(recorded.Owner, Is.EqualTo("acme"));
            Assert.That(recorded.Repository, Is.EqualTo("widgets"));
            Assert.That(recorded.Agent, Is.EqualTo("Lucy"));
            Assert.That(recorded.Category, Is.EqualTo(DecisionCategory.ProductVision));
            Assert.That(recorded.Verdict, Is.EqualTo(DecisionVerdict.Pending));
        });
    }

    [Test]
    public async Task RecordDecision_keepsTheEvidenceImagesTheBrowserToolHandedBack()
    {
        var tools = Tools(out var decisions);

        await tools.RecordDecision(
            "ws-1",
            "gui-decisions",
            "Put the reorder grip on the left of each queued row",
            "The grip sits left of the title so a thumb reaches it without covering the text.",
            "The iOS calendar does the same, and the right side is where the destructive action is.",
            evidenceArtifactIds: ["artifact-abc", "artifact-def"]);

        var images = decisions.All().Single().EvidenceImages;

        Assert.Multiple(() =>
        {
            Assert.That(
                images.Select(image => image.SourceArtifactId),
                Is.EqualTo(new[] { "artifact-abc", "artifact-def" }),
                "each picture still says which capture it came from");
            Assert.That(
                images.Select(image => image.EvidenceId),
                Is.All.Not.AnyOf("artifact-abc", "artifact-def"),
                "but the backlog keeps its own copy under its own id");
        });
    }

    [TestCase("product vision", DecisionCategory.ProductVision)]
    [TestCase("GUI", DecisionCategory.GuiDecisions)]
    [TestCase("code_architecture", DecisionCategory.CodeArchitecture)]
    public async Task RecordDecision_understandsACategoryWrittenTheObviousWays(string written, DecisionCategory expected)
    {
        var tools = Tools(out var decisions);

        await tools.RecordDecision(
            "ws-1",
            written,
            "Queue the work on the Server rather than the host",
            "The Server owns the order of work; the host only reports what is open.",
            "The host cannot assign an agent, so the order had to live somewhere else.");

        Assert.That(decisions.All().Single().Category, Is.EqualTo(expected));
    }

    [Test]
    public async Task RecordDecision_namesTheThreeCategoriesWhenItIsGivenSomethingElse()
    {
        var tools = Tools(out var decisions);

        var result = await tools.RecordDecision(
            "ws-1",
            "performance",
            "Queue the work on the Server rather than the host",
            "The Server owns the order of work; the host only reports what is open.",
            "The host cannot assign an agent, so the order had to live somewhere else.");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("product-vision").And.Contain("gui-decisions").And.Contain("code-architecture"));
            Assert.That(decisions.All(), Is.Empty);
        });
    }

    [Test]
    public async Task RecordDecision_tellsTheAgentEverythingWrongAtOnce()
    {
        // One round trip per fault would cost an agent its turn for nothing.
        var tools = Tools(out _);

        var result = await tools.RecordDecision("ws-1", "product-vision", "no", "no", "no");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("[headline]").And.Contain("[detail]").And.Contain("[evidence]"));
        });
    }

    [Test]
    public async Task RecordDecision_saysWhichDecisionARepeatMatches()
    {
        var tools = Tools(out var decisions);
        await tools.RecordDecision(
            "ws-1",
            "product-vision",
            "Queue the work on the Server rather than the host",
            "The Server owns the order of work; the host only reports what is open.",
            "The host cannot assign an agent, so the order had to live somewhere else.");
        var first = decisions.All().Single();

        var again = await tools.RecordDecision(
            "ws-1",
            "product-vision",
            "queue the work on the server, rather than the host",
            "Reached the same conclusion again later in the run.",
            "Same reasoning as before, from the other end of the loop.");

        Assert.Multiple(() =>
        {
            Assert.That(again.Succeeded, Is.False);
            Assert.That(again.Message, Does.Contain(first.Id));
            Assert.That(decisions.All(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task RecordDecision_refusesAWorkspaceWithNoRepositoryRatherThanFilingItNowhere()
    {
        var tools = Tools(out var decisions, repository: null);

        var result = await tools.RecordDecision(
            "ws-1",
            "product-vision",
            "Queue the work on the Server rather than the host",
            "The Server owns the order of work; the host only reports what is open.",
            "The host cannot assign an agent, so the order had to live somewhere else.");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("repository"));
            Assert.That(decisions.All(), Is.Empty);
        });
    }

    [Test]
    public async Task RecordDecision_refusesACaptureThatIsNotThereRatherThanAnEmptyFrame()
    {
        // A decision whose picture cannot be copied would show a blank card forever.
        var tools = Tools(out var decisions);

        var result = await tools.RecordDecision(
            "ws-1",
            "gui-decisions",
            "Put the reorder grip on the left of each queued row",
            "The grip sits left of the title so a thumb reaches it without covering the text.",
            "The iOS calendar does the same, and the right side is where the destructive action is.",
            evidenceArtifactIds: ["gone"]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("gone"));
            Assert.That(decisions.All(), Is.Empty);
        });
    }

    [Test]
    public async Task ListDecisions_handsAnAgentTheVerdictsOnItsOwnRepository()
    {
        // This is the whole point of the loop: what the owner judged good is how they want the
        // product built, and the agent should be able to read it on its next run.
        var tools = Tools(out var decisions, out var service);
        await tools.RecordDecision(
            "ws-1",
            "gui-decisions",
            "Put the reorder grip on the left of each queued row",
            "The grip sits left of the title so a thumb reaches it without covering the text.",
            "The iOS calendar does the same, and the right side is where the destructive action is.");
        service.Review(decisions.All().Single().Id, DecisionVerdict.Good, "Yes, keep this.", DateTimeOffset.UnixEpoch);

        var result = await tools.ListDecisions("ws-1");

        var listed = (IReadOnlyList<ProductDecisionDto>)result.Data!;
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(listed.Single().Verdict, Is.EqualTo("good"));
            Assert.That(listed.Single().Note, Is.EqualTo("Yes, keep this."));
        });
    }

    [Test]
    public async Task ListDecisions_narrowsToWhatTheOwnerActuallyJudged()
    {
        var tools = Tools(out var decisions, out var service);
        await tools.RecordDecision("ws-1", "gui-decisions",
            "Put the reorder grip on the left of each queued row",
            "The grip sits left of the title so a thumb reaches it.",
            "The iOS calendar does the same, and the right side is destructive.");
        await tools.RecordDecision("ws-1", "product-vision",
            "Queue the work on the Server rather than the host",
            "The Server owns the order of work; the host only reports what is open.",
            "The host cannot assign an agent, so order had to live somewhere else.");
        service.Review(decisions.All().First().Id, DecisionVerdict.Bad, null, DateTimeOffset.UnixEpoch);

        var bad = (IReadOnlyList<ProductDecisionDto>)(await tools.ListDecisions("ws-1", verdict: "bad")).Data!;
        var pending = (IReadOnlyList<ProductDecisionDto>)(await tools.ListDecisions("ws-1", verdict: "pending")).Data!;

        Assert.Multiple(() =>
        {
            Assert.That(bad, Has.Count.EqualTo(1));
            Assert.That(pending, Has.Count.EqualTo(1));
            Assert.That(bad.Single().Headline, Does.StartWith("Put the reorder grip"));
        });
    }

    [Test]
    public async Task ListDecisions_saysSoWhenAnAgentHasNothingToLearnFromYet()
    {
        var tools = Tools(out _);

        var result = await tools.ListDecisions("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Does.Contain("No product decisions"));
        });
    }

    [Test]
    public async Task GetDecision_readsOneBackInFull()
    {
        var tools = Tools(out var decisions);
        await tools.RecordDecision("ws-1", "product-vision",
            "Queue the work on the Server rather than the host",
            "The Server owns the order of work; the host only reports what is open.",
            "The host cannot assign an agent, so order had to live somewhere else.");

        var result = await tools.GetDecision(decisions.All().Single().Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(((ProductDecisionDto)result.Data!).Headline, Does.StartWith("Queue the work"));
        });
    }

    [Test]
    public async Task GetDecision_saysSoWhenThereIsNoSuchDecision()
    {
        var tools = Tools(out _);

        Assert.That((await tools.GetDecision("nope")).Succeeded, Is.False);
    }

    [Test]
    public async Task Categories_tellAnAgentWhatEachOneCovers()
    {
        var tools = Tools(out _);

        var result = await tools.Categories();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Does.Contain("product-vision"));
            Assert.That(result.Data, Is.Not.Null);
        });
    }

    private static TrainingMcpTools Tools(out ProductDecisionService decisions, string? repository = "widgets")
        => Tools(out decisions, out _, repository);

    private static TrainingMcpTools Tools(
        out ProductDecisionService decisions,
        out ProductDecisionService reviewer,
        string? repository = "widgets")
    {
        decisions = new ProductDecisionService(new MemoryDecisionStore());
        reviewer = decisions;
        return new TrainingMcpTools(new TrainingController(new TrainingService(
            decisions,
            new FixedRepository(repository),
            new MemoryEvidenceStore(),
            new StubCaptures())));
    }

    /// <summary>The repository a workspace is on, stated by the test rather than read off a forge.</summary>
    private sealed class FixedRepository(string? repository) : IDecisionRepositoryResolver
    {
        public (string Owner, string Repository)? Resolve(string workspaceId)
            => repository is null ? null : ("acme", repository);
    }

    /// <summary>Keeps the copies the backlog takes, so a test can see that it took them.</summary>
    private sealed class MemoryEvidenceStore : IDecisionEvidenceStore
    {
        private readonly Dictionary<string, (DecisionEvidenceImage Image, byte[] Bytes)> _images = [];

        public Task<DecisionEvidenceImage> SaveAsync(string sourceArtifactId, string mimeType, byte[] bytes, CancellationToken cancellationToken)
        {
            var image = new DecisionEvidenceImage($"evidence-{_images.Count}", sourceArtifactId, mimeType);
            _images[image.EvidenceId] = (image, bytes);
            return Task.FromResult(image);
        }

        public Task<(DecisionEvidenceImage Image, byte[] Bytes)?> LoadAsync(string evidenceId, CancellationToken cancellationToken)
            => Task.FromResult(_images.TryGetValue(evidenceId, out var found) ? found : ((DecisionEvidenceImage, byte[])?)null);
    }

    /// <summary>Every artifact id reads back, except the one a test says is missing.</summary>
    private sealed class StubCaptures : IDecisionEvidenceSource
    {
        public Task<(string MimeType, byte[] Bytes)?> ReadAsync(string artifactId, CancellationToken cancellationToken)
            => Task.FromResult<(string MimeType, byte[] Bytes)?>(
                artifactId == "gone" ? null : ("image/png", [1, 2, 3]));
    }

    private sealed class MemoryDecisionStore : IProductDecisionStore
    {
        private IReadOnlyList<ProductDecision> _decisions = [];

        public IReadOnlyList<ProductDecision> Read() => _decisions;

        public void Write(IReadOnlyList<ProductDecision> decisions) => _decisions = [.. decisions];
    }
}
