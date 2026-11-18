using AgentUp.Capabilities.Forgejo.Tests.Support;
using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Forgejo.Tests.Features.ForgejoCapability.E2E;

[TestFixture]
[Category("ForgejoE2E")]
[NonParallelizable]
[Timeout(180_000)]
[CancelAfter(180_000)]
public sealed class ForgejoForgeCapabilityE2ETests
{
    [OneTimeTearDown]
    public Task StopHost() => ForgejoLiveHost.StopSharedAsync();

    [Test]
    public async Task ReadAsync_returnsNoProposalWhenTheHeadBranchHasNoPull()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName("none"), CancellationToken.None);

        var snapshot = await host.Capability().ReadAsync(host.Request(repo, "main"));

        Assert.That(snapshot.Proposal, Is.Null);
        Assert.That(snapshot.Account!.Login, Is.EqualTo(ForgejoLiveHost.Owner));
    }

    [Test]
    public async Task OpenProposalAsync_opensAPullFromThePushedHeadBranch()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName("open"), CancellationToken.None);

        var proposal = await host.Capability().OpenProposalAsync(
            host.Request(repo, "topic"),
            new ForgeOpenProposalCommand("Topic", "Body", "topic", "main"));

        Assert.That(proposal.HeadRef, Is.EqualTo("topic"));
        Assert.That(proposal.BaseRef, Is.EqualTo("main"));
        Assert.That(proposal.State, Is.EqualTo("open"));
        Assert.That(proposal.Url, Does.Contain($"/pulls/{proposal.Number}"));
    }

    [Test]
    public async Task ReadAsync_returnsTheOpenProposalStatusesAndConversation()
    {
        var (host, repo, proposal) = await OpenedRepositoryAsync("read");
        await host.CreateCommitStatusAsync(
            repo.Name,
            repo.TopicSha,
            "ci",
            "success",
            $"{host.Origin}/{repo.Owner}/{repo.Name}/actions/1",
            CancellationToken.None);
        await host.Capability().CommentAsync(
            host.Request(repo, "topic"),
            new ForgeCommentCommand(proposal.Number, "please fix"));

        var snapshot = await host.Capability().ReadAsync(host.Request(repo, "topic"));

        Assert.That(snapshot.Proposal!.Number, Is.EqualTo(proposal.Number));
        Assert.That(snapshot.Checks.Single().Name, Is.EqualTo("ci"));
        Assert.That(snapshot.Checks.Single().Conclusion, Is.EqualTo("success"));
        Assert.That(snapshot.Threads.Select(thread => thread.Summary), Does.Contain("please fix"));
        Assert.That(snapshot.Mergeability, Is.Not.Null);
    }

    [Test]
    public async Task ListIssuesAsync_returnsEveryOpenIssueOnTheRepository()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName("issues"), CancellationToken.None);
        await host.CreateIssueAsync(repo.Name, "Assigned", "do it", ForgejoLiveHost.Owner, CancellationToken.None);
        await host.CreateIssueAsync(repo.Name, "Unassigned", "later", null, CancellationToken.None);

        var issues = await host.Capability().ListIssuesAsync(host.Request(repo, "main"));

        Assert.That(issues.Select(issue => issue.Title), Is.EquivalentTo(new[] { "Assigned", "Unassigned" }));
    }

    [Test]
    public async Task ListAssignedAsync_returnsOnlyIssuesAssignedToTheTokenOwner()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName("assigned"), CancellationToken.None);
        await host.CreateIssueAsync(repo.Name, "Assigned", "do it", ForgejoLiveHost.Owner, CancellationToken.None);
        await host.CreateIssueAsync(repo.Name, "Unassigned", "later", null, CancellationToken.None);

        var issues = await host.Capability().ListAssignedAsync(host.Request(repo, "main"));

        Assert.That(issues.Select(issue => issue.Title), Is.EqualTo(new[] { "Assigned" }));
    }

    [Test]
    public async Task SubmitReviewAsync_isVisibleOnTheHostingSnapshot()
    {
        var (host, repo, proposal) = await OpenedRepositoryAsync("review");

        await host.Capability().SubmitReviewAsync(
            host.Request(repo, "topic"),
            new ForgeSubmitReviewCommand(proposal.Number, "lgtm", "COMMENT"));
        var snapshot = await host.Capability().ReadAsync(host.Request(repo, "topic"));

        var reviews = snapshot.Reviews ?? [];
        Assert.That(reviews, Has.Count.EqualTo(1));
        Assert.That(reviews[0].Login, Is.EqualTo(ForgejoLiveHost.Owner));
        Assert.That(reviews[0].State, Does.Match("COMMENT(ED)?"));
    }

    [Test]
    public async Task UpdateRefAsync_movesAnUnprotectedBranchToTheRequestedSha()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName("ref"), CancellationToken.None);

        await host.Capability().UpdateRefAsync(
            host.Request(repo, "topic"),
            new ForgeUpdateRefCommand("topic", repo.MainSha));
        var sha = await host.ReadBranchShaAsync(repo.Name, "topic", CancellationToken.None);

        Assert.That(sha, Is.EqualTo(repo.MainSha));
        Assert.That(repo.TopicSha, Is.Not.EqualTo(repo.MainSha));
    }

    private static async Task<(ForgejoLiveHost Host, ForgejoLiveRepository Repo, ForgeProposal Proposal)> OpenedRepositoryAsync(
        string prefix)
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName(prefix), CancellationToken.None);
        var proposal = await host.Capability().OpenProposalAsync(
            host.Request(repo, "topic"),
            new ForgeOpenProposalCommand("Topic", "Body", "topic", "main"));
        return (host, repo, proposal);
    }

    private static string UniqueName(string prefix)
        => $"e2e-{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
