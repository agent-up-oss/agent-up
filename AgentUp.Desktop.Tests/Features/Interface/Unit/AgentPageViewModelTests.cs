using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

[TestFixture]
public sealed class AgentPageViewModelTests
{
    [Test]
    public async Task ReadAsync_fillsTheFactsAndTheApplicationsFromTheWorkspace()
    {
        var page = AgentPageComposition.Page(TeamStandingComposition.Standing(), Lucy());
        await page.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Facts.Select(fact => fact.Label), Is.EqualTo(new[] { "Task", "Proposal", "Branch", "Head" }));
            Assert.That(page.Facts.Single(fact => fact.Label == "Branch").Value, Is.EqualTo("feat/live"));
            Assert.That(page.HasFacts, Is.True);
            Assert.That(page.Applications.Single().Address, Is.EqualTo(":9100 · http"));
            Assert.That(page.Applications.Single().IsRunning, Is.True);
            Assert.That(page.HasApplications, Is.True);
            Assert.That(page.ApplicationsHeading, Is.EqualTo("APPLICATIONS · 1"));
        });
    }

    [Test]
    public async Task ReadAsync_listsTheChangedFilesRatherThanTheFoldersAroundThem()
    {
        var git = new StandingForgeFake();
        git.Changes["lucy"] = Tree();

        var page = AgentPageComposition.Page(TeamStandingComposition.Standing(), Lucy(), git);
        await page.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Changes.Select(change => change.Path), Is.EqualTo(new[] { "readme.md", "apps/grid.tsx" }));
            Assert.That(page.Changes.Select(change => change.Mark), Is.EqualTo(new[] { "A", "M" }));
            Assert.That(page.HasChanges, Is.True);
            Assert.That(page.ChangesHeading, Is.EqualTo("CHANGES · 2"));
        });
    }

    [Test]
    public async Task ReadAsync_keepsTheStandingWhenTheWorkspaceReadFails()
    {
        // The page was opened from a standing the team already holds. A workspace that cannot be
        // read says nothing about that standing, so the page is thinner rather than empty.
        var workspaces = new AgentPageWorkspaceFake();
        workspaces.Failures["lucy"] = new HttpRequestException("no route to the Server");

        var page = AgentPageComposition.Page(TeamStandingComposition.Standing(), workspaces);
        await page.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Applications, Is.Empty);
            Assert.That(page.Facts.Select(fact => fact.Label), Is.EqualTo(new[] { "Task", "Proposal", "Branch" }));
            Assert.That(page.Facts.Single(fact => fact.Label == "Branch").Value, Is.EqualTo("feat/44-restock"));
            Assert.That(page.DisplayName, Is.EqualTo("Lucy"));
            Assert.That(page.Subtitle, Is.EqualTo("Reviewer · @lucy-aup[bot] · Claude"));
        });
    }

    [Test]
    public async Task ReadAsync_saysWhyTheWorkingTreeIsMissingRatherThanShowingItClean()
    {
        var git = new StandingForgeFake();
        git.ChangeFailures["lucy"] = new HttpRequestException("the working tree could not be read");

        var page = AgentPageComposition.Page(TeamStandingComposition.Standing(), git: git);
        await page.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.Changes, Is.Empty);
            Assert.That(page.HasChangesError, Is.True);
            Assert.That(page.ChangesError, Is.EqualTo("the working tree could not be read"));
        });
    }

    [Test]
    public async Task TheEmptyChangesHint_tellsAReadInFlightApartFromACleanTree()
    {
        var page = AgentPageComposition.Page(TeamStandingComposition.Standing());

        Assert.That(page.EmptyChangesHint, Does.Contain("Reading"));

        await page.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.EmptyChangesHint, Is.EqualTo("The working tree is clean."));
            Assert.That(page.HasChangesError, Is.False);
        });
    }

    [Test]
    public void ThePage_opensOnTheOverviewAndMovesToGitOnCommand()
    {
        var page = AgentPageComposition.Page(TeamStandingComposition.Standing());

        Assert.Multiple(() =>
        {
            Assert.That(page.Section, Is.EqualTo(AgentSection.Overview));
            Assert.That(page.ShowsOverview, Is.True);
            Assert.That(page.ShowsGit, Is.False);
        });

        page.ShowGitCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(page.ShowsGit, Is.True);
            Assert.That(page.ShowsOverview, Is.False);
        });

        page.ShowOverviewCommand.Execute().Subscribe();

        Assert.That(page.ShowsOverview, Is.True);
    }

    [Test]
    public void AGateIsNamedOnThePage_soTheReasonToOpenItIsTheFirstThingRead()
    {
        var halted = TeamStandingComposition.Standing(
            kind: TeamStandingKind.Halted,
            reason: "The loop stopped on a fault.",
            diagnosticCode: "AUP-1042");

        var page = AgentPageComposition.Page(halted);

        Assert.Multiple(() =>
        {
            Assert.That(page.HasGate, Is.True);
            Assert.That(page.GateLabel, Is.EqualTo("Halted"));
            Assert.That(page.HasReason, Is.True);
            Assert.That(page.Reason, Is.EqualTo("The loop stopped on a fault."));
            Assert.That(page.HasDiagnosticCode, Is.True);
            Assert.That(page.DiagnosticCode, Is.EqualTo("AUP-1042"));
        });
    }

    [Test]
    public void AnAgentWithNothingInHand_carriesNoGateAndNoProposal()
    {
        var idle = TeamStandingComposition.Standing(
            kind: TeamStandingKind.Working,
            taskNumber: null,
            taskTitle: null,
            proposal: null,
            passed: 0);

        var page = AgentPageComposition.Page(idle);

        Assert.Multiple(() =>
        {
            Assert.That(page.HasGate, Is.False);
            Assert.That(page.HasProposal, Is.False);
            Assert.That(page.ProposalLabel, Is.Empty);
            Assert.That(page.ProposalDetail, Is.EqualTo("No proposal open on this workspace."));
            Assert.That(page.CheckSummary, Is.EqualTo("No checks reported."));
            Assert.That(page.FindingSummary, Is.EqualTo("No proposal open."));
            Assert.That(page.HasReason, Is.False);
            Assert.That(page.HasDiagnosticCode, Is.False);
        });
    }

    [Test]
    public void AnOpenProposal_isNamedWithHowItsChecksAndThreadsStand()
    {
        var page = AgentPageComposition.Page(TeamStandingComposition.Standing(failed: 1));

        Assert.Multiple(() =>
        {
            Assert.That(page.HasProposal, Is.True);
            Assert.That(page.ProposalLabel, Is.EqualTo("#431 Restock the featured rail"));
            Assert.That(page.CheckSummary, Is.EqualTo("3 passed · 1 failed"));
            Assert.That(page.FindingSummary, Is.EqualTo("2 unresolved threads"));
            Assert.That(page.HasMergeState, Is.True);
            Assert.That(page.MergeState, Is.EqualTo("clean"));
        });
    }

    [Test]
    public async Task AStoppedApplication_isNotReportedAsRunning()
    {
        var workspaces = new AgentPageWorkspaceFake();
        workspaces.Workspaces["lucy"] = new WorkspaceDtoBuilder()
            .Identified("lucy")
            .WithApplication(new ApplicationDtoBuilder().Named("Worker").Stopped())
            .Build();

        var page = AgentPageComposition.Page(TeamStandingComposition.Standing(), workspaces);
        await page.ReadAsync();

        var application = page.Applications.Single();

        Assert.Multiple(() =>
        {
            Assert.That(application.Name, Is.EqualTo("Worker"));
            Assert.That(application.IsRunning, Is.False);
            Assert.That(application.Address, Is.EqualTo("no port"));
        });
    }

    /// <summary>The workspace the standing names, as the Server would answer it.</summary>
    private static AgentPageWorkspaceFake Lucy()
    {
        var workspaces = new AgentPageWorkspaceFake();
        workspaces.Workspaces["lucy"] = new WorkspaceDtoBuilder()
            .Identified("lucy")
            .OnBranch("feat/live")
            .AtCommit("a71f093")
            .WithApplication(new ApplicationDtoBuilder().Named("Storefront").WithPort(9100))
            .Build();
        return workspaces;
    }

    private static GitChangeTreeDto Tree()
        => new(
            "lucy",
            "feat/44-restock",
            2,
            new GitChangeDirectoryDto(
                string.Empty,
                string.Empty,
                [new GitChangeDirectoryDto("apps", "apps", [], [new GitChangeFileDto("grid.tsx", "apps/grid.tsx", "Modified")])],
                [new GitChangeFileDto("readme.md", "readme.md", "Added")]));
}
