using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Providers;
using AgentUp.Sdk.Common;
using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;

public sealed class ForgejoForgeCapability : IForgeCapability
{
    public const string PackageId = "forgejo";
    public const string PackageVersion = "1.0.0";

    private readonly ForgejoApiProvider _api;

    public ForgejoForgeCapability()
        : this(new ForgejoHostConfigurationProvider().Read())
    {
    }

    public ForgejoForgeCapability(ForgeHostConfiguration host)
        : this(new ForgejoApiProvider(apiBaseUrl: host.ApiBaseUrl), host)
    {
    }

    public ForgejoForgeCapability(HttpMessageHandler handler, ForgeHostConfiguration? host = null)
        : this(new ForgejoApiProvider(handler, host?.ApiBaseUrl), host ?? new ForgejoHostConfigurationProvider().Read())
    {
    }

    public ForgejoForgeCapability(ForgejoApiProvider api, ForgeHostConfiguration? host = null)
    {
        _api = api;
        Host = host ?? new ForgejoHostConfigurationProvider().Read();
    }

    public CapabilityIdentity Identity { get; } = new(PackageId, PackageVersion, "Forgejo", "agent-up");

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
