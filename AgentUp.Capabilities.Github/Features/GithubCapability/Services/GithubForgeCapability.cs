using AgentUp.Capabilities.Github.Features.GithubCapability.Providers;
using AgentUp.Sdk.Common;
using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Github.Features.GithubCapability.Services;

public sealed class GithubForgeCapability : IForgeCapability
{
    public const string PackageId = "github";
    public const string PackageVersion = "1.0.0";

    private readonly GithubApiProvider _api;

    public GithubForgeCapability()
        : this(new GithubApiProvider())
    {
    }

    public GithubForgeCapability(HttpMessageHandler handler)
        : this(new GithubApiProvider(handler))
    {
    }

    public GithubForgeCapability(GithubApiProvider api) => _api = api;

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, "GitHub", "agent-up");

    public Task<ForgeHostSnapshot> ReadAsync(ForgeRequest request, CancellationToken cancellationToken = default)
        => _api.ReadAsync(request, cancellationToken);

    public Task<IReadOnlyList<ForgeAssignedIssue>> ListAssignedAsync(
        ForgeRequest request,
        CancellationToken cancellationToken = default)
        => _api.ListAssignedAsync(request, cancellationToken);

    public Task<ForgeProposal> OpenProposalAsync(
        ForgeRequest request,
        ForgeOpenProposalCommand command,
        CancellationToken cancellationToken = default)
        => _api.OpenProposalAsync(request, command, cancellationToken);

    public Task UpdateRefAsync(
        ForgeRequest request,
        ForgeUpdateRefCommand command,
        CancellationToken cancellationToken = default)
        => _api.UpdateRefAsync(request, command, cancellationToken);

    public Task CommentAsync(
        ForgeRequest request,
        ForgeCommentCommand command,
        CancellationToken cancellationToken = default)
        => _api.CommentAsync(request, command, cancellationToken);

    public Task SubmitReviewAsync(
        ForgeRequest request,
        ForgeSubmitReviewCommand command,
        CancellationToken cancellationToken = default)
        => _api.SubmitReviewAsync(request, command, cancellationToken);
}
