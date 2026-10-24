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
        : this(new GithubHostConfigurationProvider().Read())
    {
    }

    public GithubForgeCapability(ForgeHostConfiguration host)
        : this(new GithubApiProvider(apiBaseUrl: host.ApiBaseUrl), host)
    {
    }

    public GithubForgeCapability(HttpMessageHandler handler, ForgeHostConfiguration? host = null)
        : this(
            new GithubApiProvider(handler, (host ??= new GithubHostConfigurationProvider().Read()).ApiBaseUrl),
            host)
    {
    }

    public GithubForgeCapability(GithubApiProvider api, ForgeHostConfiguration? host = null)
    {
        _api = api;
        Host = host ?? new GithubHostConfigurationProvider().Read();
    }

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, "GitHub", "agent-up");

    public ForgeHostConfiguration Host { get; }

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
