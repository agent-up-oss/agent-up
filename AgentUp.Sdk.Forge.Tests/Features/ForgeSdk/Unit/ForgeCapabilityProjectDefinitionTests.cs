using AgentUp.Sdk.Common;
using AgentUp.Sdk.Forge;

namespace AgentUp.Sdk.Forge.Tests.Features.ForgeSdk.Unit;

[TestFixture]
public sealed class ForgeCapabilityProjectDefinitionTests
{
    [Test]
    public void CreateProject_registers_the_forge_implementation()
    {
        var project = new SampleForgeProject().CreateProject();

        Assert.That(project.Registrations.Single().ImplementationType, Is.EqualTo(typeof(SampleForgeCapability)));
    }

    [Test]
    public void Identity_is_the_public_module_id()
    {
        Assert.That(new SampleForgeCapability().Identity.Id, Is.EqualTo("sample-forge"));
    }

    [Test]
    public void Host_carries_module_owned_endpoints()
    {
        var host = new ForgeHostConfiguration(
            "https://git.example.test/api/v1/",
            "https://git.example.test/login/oauth/authorize",
            "https://git.example.test/login/oauth/access_token");

        Assert.That(host.ApiBaseUrl, Does.EndWith("/api/v1/"));
        Assert.That(host.OAuthAuthorizeUrl, Does.Contain("authorize"));
        Assert.That(host.OAuthTokenUrl, Does.Contain("access_token"));
        Assert.That(new SampleForgeCapability().Host.ApiBaseUrl, Does.StartWith("https://example.test/"));
    }

    private sealed class SampleForgeProject : ForgeCapabilityProjectDefinition
    {
        protected override void Register(ForgeCapabilityRegistrationBuilder registry)
            => registry.Add<SampleForgeCapability>();
    }

    private sealed class SampleForgeCapability : IForgeCapability
    {
        public CapabilityIdentity Identity { get; } = new("sample-forge", "1.0.0", "Sample", "agent-up");

        public ForgeHostConfiguration Host { get; } = new(
            "https://example.test/api/",
            "https://example.test/login/oauth/authorize",
            "https://example.test/login/oauth/access_token");

        public Task<ForgeHostSnapshot> ReadAsync(ForgeRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ForgeHostSnapshot(null, [], [], null));

        public Task<IReadOnlyList<ForgeIssue>> ListAssignedAsync(
            ForgeRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ForgeIssue>>([]);

        public Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(
            ForgeRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ForgeIssue>>([]);

        public Task<ForgeProposal> OpenProposalAsync(
            ForgeRequest request,
            ForgeOpenProposalCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ForgeProposal("1", "1", command.Title, "https://example.test/1", command.HeadRef, command.BaseRef, "open"));

        public Task UpdateRefAsync(
            ForgeRequest request,
            ForgeUpdateRefCommand command,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CommentAsync(
            ForgeRequest request,
            ForgeCommentCommand command,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubmitReviewAsync(
            ForgeRequest request,
            ForgeSubmitReviewCommand command,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
