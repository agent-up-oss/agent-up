using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Forge.Tests.Features.ForgeSdk.Unit;

/// <summary>
/// The writes a team needs beyond the original contract have defaults, so a host module that
/// cannot assign, request a reviewer, or store a file still loads and simply does none of them.
/// </summary>
[TestFixture]
public sealed class ForgeCapabilityDefaultsTests
{
    [Test]
    public async Task A_host_without_team_writes_leaves_issues_and_proposals_as_they_are()
    {
        IForgeCapability forge = new MinimalForge();
        var request = new ForgeRequest("token", "acme", "widgets", "main");

        await forge.AssignIssueAsync(request, 3, "bob");
        await forge.RequestReviewAsync(request, "7", "hector");
        var attachment = await forge.AttachAsync(request, "7", "shot.png", "image/png", [1, 2, 3]);

        Assert.That(attachment, Is.Null, "no upload API means no link to put in a comment");
    }

    [Test]
    public void A_review_can_carry_findings_at_code_locations()
    {
        var review = new ForgeSubmitReviewCommand("7", "Two things.", "REQUEST_CHANGES",
            [new ForgeReviewComment("src/greeting.js", 1, "Keep the comma."), new ForgeReviewComment("test/page.test.js", 8, "Assert the new headline.")]);

        Assert.That(review.Comments!.Select(comment => $"{comment.Path}:{comment.Line}"), Is.EqualTo(new[] { "src/greeting.js:1", "test/page.test.js:8" }));
    }

    private sealed class MinimalForge : IForgeCapability
    {
        public CapabilityIdentity Identity { get; } = new("minimal", "1.0.0", "Minimal", "test");

        public string Kind => CapabilityKind.Forge;

        public IReadOnlyList<CapabilitySoftware> Software { get; } = [];

        public IReadOnlyList<CapabilitySettingSpec> Settings { get; } = [];

        public ForgeHostConfiguration Host { get; } = new("https://host.test/api/", "", "");

        public Task<ForgeHostSnapshot> ReadAsync(ForgeRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ForgeHostSnapshot(null, [], [], null));

        public Task<IReadOnlyList<ForgeIssue>> ListAssignedAsync(ForgeRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ForgeIssue>>([]);

        public Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(ForgeRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ForgeIssue>>([]);

        public Task<ForgeProposal> OpenProposalAsync(ForgeRequest request, ForgeOpenProposalCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateRefAsync(ForgeRequest request, ForgeUpdateRefCommand command, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CommentAsync(ForgeRequest request, ForgeCommentCommand command, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubmitReviewAsync(ForgeRequest request, ForgeSubmitReviewCommand command, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
