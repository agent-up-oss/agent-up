using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Services;

namespace AgentUp.Server.Features.Forge.Controllers;

public sealed class ForgeController(ForgeService service, GithubAppConnectionService? githubApps = null)
{
    public Task<ForgeHostingDto?> GetHostingAsync(string workspaceId, CancellationToken cancellationToken = default)
        => service.GetHostingAsync(workspaceId, cancellationToken);

    public Task<ForgeHttpOutcome> StartLoginAsync(
        string workspaceId,
        string callbackOrigin,
        CancellationToken cancellationToken = default)
        => service.StartLoginAsync(workspaceId, callbackOrigin, cancellationToken);

    public string GithubRegistrationPage(string state)
        => RequireGithubApps().RegistrationPage(state);

    public Task<string?> CompleteGithubManifestAsync(string code, string state, CancellationToken cancellationToken = default)
        => RequireGithubApps().CompleteManifestAsync(code, state, cancellationToken);

    public Task<bool> CompleteGithubInstallationAsync(
        string key, long installationId, CancellationToken cancellationToken = default)
        => RequireGithubApps().CompleteInstallationAsync(key, installationId, cancellationToken);

    private GithubAppConnectionService RequireGithubApps()
        => githubApps ?? throw new InvalidOperationException("GitHub App connections are unavailable.");

    public Task<bool> CompleteLoginAsync(string code, string state, CancellationToken cancellationToken = default)
        => service.CompleteLoginAsync(code, state, cancellationToken);

    public bool HasCredential(string workspaceId)
        => service.HasCredential(workspaceId);

    public bool HasAnyCredential()
        => service.HasAnyCredential();

    public bool Logout(string workspaceId)
        => service.Logout(workspaceId);

    public Task<IReadOnlyList<ForgeAssignedIssue>> ListAssignedAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
        => service.ListAssignedAsync(workspaceId, cancellationToken);

    public Task<ForgeProposal?> OpenProposalAsync(
        string workspaceId,
        ForgeOpenProposalRequest command,
        CancellationToken cancellationToken = default)
        => service.OpenProposalAsync(workspaceId, command, cancellationToken);

    public Task<ForgeHttpOutcome> UpdateRefAsync(string workspaceId, ForgeUpdateRefRequest command, CancellationToken cancellationToken = default)
        => service.UpdateRefAsync(workspaceId, command, cancellationToken);

    public Task CommentAsync(string workspaceId, ForgeCommentRequest command, CancellationToken cancellationToken = default)
        => service.CommentAsync(workspaceId, command, cancellationToken);

    public Task SubmitReviewAsync(string workspaceId, ForgeReviewRequest command, CancellationToken cancellationToken = default)
        => service.SubmitReviewAsync(workspaceId, command, cancellationToken);
}
