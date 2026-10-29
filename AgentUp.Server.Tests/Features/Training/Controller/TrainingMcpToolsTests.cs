using AgentUp.Server.Features.Training.Controllers;
using AgentUp.Server.Features.Training.DTOs;
using AgentUp.Server.Features.Training.Interfaces;
using AgentUp.Server.Features.Training.Models;
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

        Assert.That(decisions.All().Single().EvidenceArtifactIds, Is.EqualTo(new[] { "artifact-abc", "artifact-def" }));
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

    private static TrainingMcpTools Tools(
        out ProductDecisionService decisions,
        string? repository = "widgets")
    {
        decisions = new ProductDecisionService(new MemoryDecisionStore());
        return new TrainingMcpTools(new TrainingController(new TrainingService(decisions, new FixedRepository(repository))));
    }

    /// <summary>The repository a workspace is on, stated by the test rather than read off a forge.</summary>
    private sealed class FixedRepository(string? repository) : IDecisionRepositoryResolver
    {
        public (string Owner, string Repository)? Resolve(string workspaceId)
            => repository is null ? null : ("acme", repository);
    }

    private sealed class MemoryDecisionStore : IProductDecisionStore
    {
        private IReadOnlyList<ProductDecision> _decisions = [];

        public IReadOnlyList<ProductDecision> Read() => _decisions;

        public void Write(IReadOnlyList<ProductDecision> decisions) => _decisions = [.. decisions];
    }
}
