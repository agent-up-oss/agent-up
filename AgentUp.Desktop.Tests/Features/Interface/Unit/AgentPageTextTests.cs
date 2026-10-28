using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

[TestFixture]
public sealed class AgentPageTextTests
{
    [Test]
    public void Facts_answerWhatTheWorkIsWhatWasProposedAndWhereItWasBuilt()
    {
        var facts = AgentPageText.Facts(Standing(), "feat/44-restock", "a71f093");

        Assert.That(
            facts.Select(fact => (fact.Label, fact.Value)),
            Is.EqualTo(new[]
            {
                ("Task", "#44 · Restock featured products"),
                ("Proposal", "#431 · lucy-aup[bot]"),
                ("Branch", "feat/44-restock"),
                ("Head", "a71f093"),
            }));
    }

    [Test]
    public void Facts_leaveOutWhatTheServerDidNotAnswerRatherThanCarryingItEmpty()
    {
        // A blank row reads as an answer of nothing, which is not the same as a question nobody asked.
        var bare = Standing(taskNumber: null, proposal: null) with { Branch = string.Empty };

        Assert.That(AgentPageText.Facts(bare, null, null), Is.Empty);
    }

    [Test]
    public void Facts_prefersTheBranchTheWorkspaceReportsOverTheOneTheStandingCarried()
    {
        var facts = AgentPageText.Facts(Standing(), "feat/live", null);

        Assert.That(facts.Single(fact => fact.Label == "Branch").Value, Is.EqualTo("feat/live"));
    }

    [Test]
    public void Address_namesThePortAndWhatSpeaksThere()
    {
        var application = new ApplicationDtoBuilder()
            .Named("Storefront")
            .WithPort(new PortMappingDtoBuilder().AllocatedTo(9100).WithProtocol("http"))
            .Build();

        Assert.That(AgentPageText.Address(application), Is.EqualTo(":9100 · http"));
    }

    [Test]
    public void Address_saysThereIsNoPortRatherThanShowingABlank()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                AgentPageText.Address(new ApplicationDtoBuilder().Build()),
                Is.EqualTo("no port"));
            Assert.That(
                AgentPageText.Address(new ApplicationDtoBuilder().OfKind("Desktop").Build()),
                Is.EqualTo("desktop · no port"));
        });
    }

    [Test]
    public void FindingSummary_saysWhetherAnythingIsWaitingOnAReader()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AgentPageText.FindingSummary(Standing()), Is.EqualTo("2 unresolved threads"));
            Assert.That(AgentPageText.FindingSummary(Standing(threads: 1)), Is.EqualTo("1 unresolved thread"));
            Assert.That(AgentPageText.FindingSummary(Standing(threads: 0)), Is.EqualTo("No unresolved threads."));
        });
    }

    [Test]
    public void Summaries_areOnlyWorthCountingOnceAProposalIsOpen()
    {
        var none = Standing(proposal: null);

        Assert.Multiple(() =>
        {
            Assert.That(AgentPageText.FindingSummary(none), Is.EqualTo("No proposal open."));
            Assert.That(AgentPageText.CheckSummary(none), Is.EqualTo("No checks reported."));
        });
    }

    [Test]
    public void CheckSummary_namesOnlyTheStatesThatHappened()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AgentPageText.CheckSummary(Standing()), Is.EqualTo("3 passed"));
            Assert.That(
                AgentPageText.CheckSummary(Standing(failed: 1, running: 2)),
                Is.EqualTo("3 passed · 1 failed · 2 running"));
        });
    }

    [Test]
    public void Files_takeTheChangedFilesOutOfTheDirectoryTree()
    {
        var tree = new GitChangeTreeDto(
            "lucy",
            "feat/44-restock",
            2,
            new GitChangeDirectoryDto(
                string.Empty,
                string.Empty,
                [new GitChangeDirectoryDto("apps", "apps", [], [new GitChangeFileDto("grid.tsx", "apps/grid.tsx", "Modified")])],
                [new GitChangeFileDto("readme.md", "readme.md", "Added")]));

        Assert.That(
            AgentPageText.Files(tree).Select(file => (file.Path, file.Status)),
            Is.EqualTo(new[] { ("readme.md", "Added"), ("apps/grid.tsx", "Modified") }));
    }

    [Test]
    public void Files_readAWorkspaceWithNoTreeAsNothingChanged()
    {
        Assert.That(AgentPageText.Files(null), Is.Empty);
    }

    [Test]
    public void Subtitle_readsAsTheRoleAndTheAccountTheAgentActsAs()
    {
        Assert.That(AgentPageText.Subtitle(Standing()), Is.EqualTo("Reviewer · @lucy-aup[bot] · Claude"));
    }

    [Test]
    public void Subtitle_fallsBackToWhatTheLoopIsDoingWhenThereIsNoRoleOrAccount()
    {
        var bare = Standing() with { Role = TeamRoleKind.None, Identity = null, Agent = null };

        Assert.That(AgentPageText.Subtitle(bare), Is.EqualTo("Running"));
    }

    private static TeamStanding Standing(
        int? taskNumber = 44,
        string? proposal = "431",
        int threads = 2,
        int failed = 0,
        int running = 0)
        => TeamStandingComposition.Standing(
            taskNumber: taskNumber,
            proposal: proposal,
            threads: threads,
            failed: failed,
            running: running);
}
