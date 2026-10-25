using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Capabilities.Interfaces;
using Microsoft.AspNetCore.Http;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Forge.Services;

public sealed class ForgeService(
    WorkspaceQueryController workspaces,
    IEnabledCapabilityPackages packages,
    IForgeCredentialStore credentials,
    IForgeGitRemoteResolver remotes,
    IForgeOAuthSettings oauth,
    IForgeOAuthStateStore states,
    IForgeOAuthExchange exchange,
    GithubAppConnectionService? githubApps = null)
{
    public async Task<ForgeHostingDto?> GetHostingAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        var enabledForges = packages.ListForges();
        if (enabledForges.Count == 0)
            return NotInstalled();

        var remote = remotes.Resolve(workspace.WorktreePath);
        if (remote is null)
            return NoRemote(enabledForges[0].Identity.Id);

        var forge = ResolveForge(enabledForges, remote);

        try
        {
            var token = await CredentialAsync(workspaceId, forge, remote, cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
                return NeedsCredential(forge.Identity.Id, IsLoginConfigured(forge.Identity.Id));

            var snapshot = await forge.ReadAsync(
                new ForgeRequest(token, remote.Owner, remote.Repository, remote.Branch),
                cancellationToken);
            // The workspace has an account whether or not a proposal is open, so both answers carry it.
            var account = ToAccount(snapshot.Account);
            if (snapshot.Proposal is null)
                return NoProposal(forge.Identity.Id, account);

            return Ready(
                forge.Identity.Id,
                ToProposal(snapshot.Proposal),
                [.. snapshot.Checks.Select(ToCheck)],
                [.. snapshot.Threads.Select(ToThread)],
                snapshot.Mergeability is null
                    ? null
                    : new ForgeMergeabilityDto(snapshot.Mergeability.Mergeable, snapshot.Mergeability.State),
                account,
                [.. (snapshot.Reviews ?? []).Select(ToReview)]);
        }
        catch (HttpRequestException exception)
        {
            return Unreachable(forge.Identity.Id, exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Unreachable(forge.Identity.Id, exception.Message);
        }
    }

    public async Task<ForgeHttpOutcome> StartLoginAsync(
        string workspaceId,
        string callbackOrigin,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return new ForgeHttpOutcome(StatusCodes.Status404NotFound, null);

        var remote = remotes.Resolve(workspace.WorktreePath);
        var forge = remote is null ? packages.ListForges().FirstOrDefault() : ResolveForge(packages.ListForges(), remote);
        if (forge is null)
            return new ForgeHttpOutcome(
                StatusCodes.Status503ServiceUnavailable,
                new { title = ForgeHostingCopy.OAuthUnavailableTitle, detail = ForgeHostingCopy.MissingForgeDetail });

        if (UsesGithubApp(forge) && remote is null)
            return new ForgeHttpOutcome(
                StatusCodes.Status409Conflict,
                new { title = "Repository remote required", detail = ForgeHostingCopy.NoRemoteDetail });

        if (UsesGithubApp(forge))
            return new ForgeHttpOutcome(
                StatusCodes.Status200OK,
                await (githubApps ?? throw new InvalidOperationException("GitHub App connections are unavailable."))
                    .StartAsync(
                        workspaceId,
                        workspace.DisplayName,
                        remote!.Owner,
                        forge.Host,
                        callbackOrigin,
                        cancellationToken));

        var configuration = oauth.Read();
        if (!configuration.IsConfigured)
            return new ForgeHttpOutcome(
                StatusCodes.Status503ServiceUnavailable,
                new { title = ForgeHostingCopy.OAuthUnavailableTitle, detail = ForgeHostingCopy.OAuthMissingDetail });

        var authorizeUrl = forge.Host.OAuthAuthorizeUrl;
        if (string.IsNullOrWhiteSpace(authorizeUrl))
            return new ForgeHttpOutcome(
                StatusCodes.Status503ServiceUnavailable,
                new { title = ForgeHostingCopy.OAuthUnavailableTitle, detail = ForgeHostingCopy.OAuthMissingDetail });

        var state = states.Issue(workspaceId);
        var separator = authorizeUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var url =
            authorizeUrl
            + separator
            + "response_type=code"
            + $"&client_id={Uri.EscapeDataString(configuration.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(configuration.RedirectUri)}"
            + $"&state={Uri.EscapeDataString(state)}"
            + "&scope=repo";
        return new ForgeHttpOutcome(StatusCodes.Status200OK, new ForgeLoginStartDto(url));
    }

    public string? GithubRegistrationPage(string state)
        => githubApps?.RegistrationPage(state);

    public Task<string?> CompleteGithubManifestAsync(
        string code, string state, CancellationToken cancellationToken = default)
        => githubApps is null
            ? Task.FromResult<string?>(null)
            : githubApps.CompleteManifestAsync(code, state, cancellationToken);

    public Task<bool> CompleteGithubInstallationAsync(
        string key, long installationId, CancellationToken cancellationToken = default)
        => githubApps is null
            ? Task.FromResult(false)
            : githubApps.CompleteInstallationAsync(key, installationId, cancellationToken);

    public async Task<bool> CompleteLoginAsync(string code, string state, CancellationToken cancellationToken)
    {
        var workspaceId = states.Consume(state);
        if (workspaceId is null || workspaces.GetById(workspaceId) is null)
            return false;

        var remote = remotes.Resolve(workspaces.GetById(workspaceId)!.WorktreePath);
        var forge = remote is null ? packages.ListForges().FirstOrDefault() : ResolveForge(packages.ListForges(), remote);
        if (forge is null)
            return false;

        credentials.Write(
            workspaceId,
            forge.Identity.Id,
            await exchange.ExchangeAsync(code, forge.Host.OAuthTokenUrl, cancellationToken));
        return true;
    }

    public bool HasCredential(string workspaceId)
    {
        var workspace = workspaces.GetById(workspaceId);
        var remote = workspace is null ? null : remotes.Resolve(workspace.WorktreePath);
        var forge = remote is null ? packages.ListForges().FirstOrDefault() : ResolveForge(packages.ListForges(), remote);
        return forge is not null && (UsesGithubApp(forge)
            ? githubApps?.IsConnected(workspaceId) == true
            : !string.IsNullOrWhiteSpace(credentials.Read(workspaceId, forge.Identity.Id)));
    }

    public bool HasAnyCredential()
        => workspaces.GetAll().Any(workspace => HasCredential(workspace.Id));

    public bool Logout(string workspaceId)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return false;

        var remote = remotes.Resolve(workspace.WorktreePath);
        var forge = remote is null ? packages.ListForges().FirstOrDefault() : ResolveForge(packages.ListForges(), remote);
        if (forge is not null)
        {
            if (UsesGithubApp(forge))
                githubApps!.Disconnect(workspaceId);
            else
                credentials.Delete(workspaceId, forge.Identity.Id);
        }
        return true;
    }

    public async Task<IReadOnlyList<ForgeIssue>> ListAssignedAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var request = await RequestForAsync(workspaceId, cancellationToken);
        if (request is null)
            return [];

        try
        {
            return await request.Value.Forge.ListAssignedAsync(request.Value.Request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    /// <summary>
    /// Every open issue on the workspace's own repository. This is what the Tasks tab offers to
    /// pick from; nothing here is worked on until it is put in the queue.
    /// </summary>
    public async Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var request = await RequestForAsync(workspaceId, cancellationToken);
        if (request is null)
            return [];

        try
        {
            return await request.Value.Forge.ListIssuesAsync(request.Value.Request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<ForgeProposal?> OpenProposalAsync(
        string workspaceId,
        ForgeOpenProposalRequest command,
        CancellationToken cancellationToken)
        => await MutateAsync(
            workspaceId,
            (forge, request) => forge.OpenProposalAsync(
                request,
                new ForgeOpenProposalCommand(command.Title, command.Body, command.HeadRef, command.BaseRef),
                cancellationToken),
            cancellationToken);

    public async Task<ForgeHttpOutcome> UpdateRefAsync(
        string workspaceId,
        ForgeUpdateRefRequest command,
        CancellationToken cancellationToken)
    {
        var request = await RequestForAsync(workspaceId, cancellationToken);
        if (request is null)
            return new ForgeHttpOutcome(StatusCodes.Status404NotFound, null);

        try
        {
            await request.Value.Forge.UpdateRefAsync(
                request.Value.Request,
                new ForgeUpdateRefCommand(command.Ref, command.Sha),
                cancellationToken);
            return new ForgeHttpOutcome(StatusCodes.Status204NoContent, null);
        }
        catch (ForgeProtectedRefException exception)
        {
            return new ForgeHttpOutcome(
                StatusCodes.Status400BadRequest,
                new { title = "Protected ref", detail = exception.Message });
        }
    }

    public async Task CommentAsync(string workspaceId, ForgeCommentRequest command, CancellationToken cancellationToken)
        => await MutateAsync(
            workspaceId,
            (forge, request) => forge.CommentAsync(request, new ForgeCommentCommand(command.ProposalNumber, command.Body), cancellationToken),
            cancellationToken);

    public async Task SubmitReviewAsync(string workspaceId, ForgeReviewRequest command, CancellationToken cancellationToken)
        => await MutateAsync(
            workspaceId,
            (forge, request) => forge.SubmitReviewAsync(
                request,
                new ForgeSubmitReviewCommand(command.ProposalNumber, command.Body, command.Event),
                cancellationToken),
            cancellationToken);

    private async Task<T?> MutateAsync<T>(
        string workspaceId,
        Func<IForgeCapability, ForgeRequest, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var request = await RequestForAsync(workspaceId, cancellationToken);
        if (request is null)
            return default;
        return await action(request.Value.Forge, request.Value.Request);
    }

    private Task MutateAsync(
        string workspaceId,
        Func<IForgeCapability, ForgeRequest, Task> action,
        CancellationToken cancellationToken)
        => MutateAsync(workspaceId, async (forge, request) =>
        {
            await action(forge, request);
            return true;
        }, cancellationToken);

    private async Task<(IForgeCapability Forge, ForgeRequest Request)?> RequestForAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        var remote = remotes.Resolve(workspace.WorktreePath);
        if (remote is null)
            return null;
        var forge = ResolveForge(packages.ListForges(), remote);
        if (forge is null)
            return null;
        var token = await CredentialAsync(workspaceId, forge, remote, cancellationToken);
        if (string.IsNullOrWhiteSpace(token)) return null;

        return (forge, new ForgeRequest(token, remote.Owner, remote.Repository, remote.Branch));
    }

    private Task<string?> CredentialAsync(
        string workspaceId,
        IForgeCapability forge,
        ForgeGitRemote remote,
        CancellationToken cancellationToken)
        => UsesGithubApp(forge)
            ? githubApps!.TokenAsync(workspaceId, remote.Repository, cancellationToken)
            : Task.FromResult(credentials.Read(workspaceId, forge.Identity.Id));

    private bool IsLoginConfigured(string moduleId)
        => moduleId.Equals("github", StringComparison.OrdinalIgnoreCase) && githubApps is not null
           || oauth.Read().IsConfigured;

    private bool UsesGithubApp(IForgeCapability forge)
        => githubApps is not null
           && forge.Identity.Id.Equals("github", StringComparison.OrdinalIgnoreCase);

    private static IForgeCapability? ResolveForge(
        IReadOnlyList<IForgeCapability> enabledForges,
        ForgeGitRemote remote)
    {
        if (enabledForges.Count == 0) return null;
        var remoteHost = RemoteHost(remote.RemoteUrl);
        return enabledForges.FirstOrDefault(forge => HostMatches(forge.Host, remoteHost))
               ?? enabledForges[0];
    }

    private static bool HostMatches(ForgeHostConfiguration host, string? remoteHost)
    {
        if (string.IsNullOrWhiteSpace(remoteHost)) return false;
        return Host(host.ApiBaseUrl).Equals(remoteHost, StringComparison.OrdinalIgnoreCase)
               || Host(host.OAuthAuthorizeUrl).Equals(remoteHost, StringComparison.OrdinalIgnoreCase);
    }

    private static string Host(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;

    private static string? RemoteHost(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)) return uri.Host;
        var at = value.IndexOf('@');
        var colon = value.IndexOf(':', at + 1);
        return at >= 0 && colon > at ? value[(at + 1)..colon] : null;
    }

    public Task<ForgeHttpOutcome> StartLoginAsync(string workspaceId)
        => StartLoginAsync(workspaceId, "http://localhost");

    private static ForgeHostingDto NotInstalled()
        => new(
            ForgeHostingCopy.NotInstalled,
            null,
            null,
            [],
            [],
            null,
            ForgeHostingCopy.NotInstalledDetail,
            ForgeHostingCopy.NotInstalledRemedy);

    private static ForgeHostingDto NeedsCredential(string moduleId, bool oauthConfigured)
        => oauthConfigured
            ? new(
                ForgeHostingCopy.NeedsCredential,
                moduleId,
                null,
                [],
                [],
                null,
                ForgeHostingCopy.NeedsCredentialDetail,
                ForgeHostingCopy.NeedsCredentialRemedy)
            : new(
                ForgeHostingCopy.NeedsCredential,
                moduleId,
                null,
                [],
                [],
                null,
                ForgeHostingCopy.OAuthMissingDetail,
                ForgeHostingCopy.OAuthMissingRemedy);

    private static ForgeHostingDto NoRemote(string moduleId)
        => new(
            ForgeHostingCopy.NoRemote,
            moduleId,
            null,
            [],
            [],
            null,
            ForgeHostingCopy.NoRemoteDetail,
            ForgeHostingCopy.NoRemoteRemedy);

    private static ForgeHostingDto Unreachable(string? moduleId, string? detail)
        => new(
            ForgeHostingCopy.Unreachable,
            moduleId,
            null,
            [],
            [],
            null,
            string.IsNullOrWhiteSpace(detail) ? ForgeHostingCopy.UnreachableDetail : detail,
            ForgeHostingCopy.UnreachableRemedy);

    private static ForgeHostingDto NoProposal(string moduleId, ForgeAccountDto? account)
        => new(
            ForgeHostingCopy.NoProposal,
            moduleId,
            null,
            [],
            [],
            null,
            ForgeHostingCopy.NoProposalDetail,
            null,
            account);

    private static ForgeHostingDto Ready(
        string moduleId,
        ForgeProposalDto proposal,
        IReadOnlyList<ForgeCheckDto> checks,
        IReadOnlyList<ForgeReviewThreadDto> threads,
        ForgeMergeabilityDto? mergeability,
        ForgeAccountDto? account,
        IReadOnlyList<ForgeReviewDto> reviews)
        => new(ForgeHostingCopy.Ready, moduleId, proposal, checks, threads, mergeability, null, null, account, reviews);

    private static ForgeReviewDto ToReview(ForgeReview review)
        => new(review.Login, review.State);

    private static ForgeAccountDto? ToAccount(ForgeAccount? account)
        => account is null ? null : new ForgeAccountDto(account.Login, account.DisplayName, account.IsBot, account.Url);

    private static ForgeProposalDto ToProposal(ForgeProposal proposal)
        => new(proposal.Id, proposal.Number, proposal.Title, proposal.Url, proposal.HeadRef, proposal.BaseRef, proposal.State);

    private static ForgeCheckDto ToCheck(ForgeCheck check)
        => new(check.Name, check.Status, check.Conclusion, check.Url);

    private static ForgeReviewThreadDto ToThread(ForgeReviewThread thread)
        => new(thread.Id, thread.Url, thread.Summary, thread.IsResolved);
}
