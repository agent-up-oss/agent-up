using AgentUp.Server.Features.Agents.Controllers;
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
    GithubAppConnectionService? githubApps = null,
    TeamMemberController? members = null,
    IForgeGitTransport? transport = null,
    TimeProvider? time = null)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _refreshes = new(StringComparer.Ordinal);

    /// <summary>The only branches the Server pushes on a team member's behalf.</summary>
    public const string TaskBranchPrefix = "agent-up/";

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
            try
            {
                return await ReadHostingAsync(forge, token, remote, cancellationToken);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // The host no longer accepts this workspace's credential: signing in again is the
                // remedy, not checking the host URL.
                return NeedsCredential(forge.Identity.Id, IsLoginConfigured(forge.Identity.Id));
            }
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

    private async Task<ForgeHostingDto> ReadHostingAsync(IForgeCapability forge, string token, ForgeGitRemote remote, CancellationToken cancellationToken)
    {
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

        credentials.WriteGrant(
            workspaceId,
            forge.Identity.Id,
            await exchange.ExchangeGrantAsync(code, forge.Host.OAuthTokenUrl, cancellationToken));
        return true;
    }

    /// <summary>
    /// The sign-in callback, answered as the page the browser lands on: signed in, refused by the
    /// host with its reason, or an unknown sign-in.
    /// </summary>
    public async Task<ForgeHttpOutcome> CompleteLoginOutcomeAsync(string code, string state, CancellationToken cancellationToken)
    {
        try
        {
            return await CompleteLoginAsync(code, state, cancellationToken)
                ? new ForgeHttpOutcome(StatusCodes.Status200OK, new { signedIn = true })
                : new ForgeHttpOutcome(StatusCodes.Status404NotFound, null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            return new ForgeHttpOutcome(
                StatusCodes.Status502BadGateway,
                new { title = "Forge sign-in failed", detail = exception.Message });
        }
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

    /// <summary>
    /// A review raises threads another team member must address, so the workspace submitting it
    /// has to be a reviewer. The check sits here rather than in a client so REST, an in-process
    /// agent, and any later caller meet the same rule.
    /// </summary>
    public async Task<ForgeReviewOutcome> SubmitReviewAsync(string workspaceId, ForgeReviewRequest command, CancellationToken cancellationToken)
    {
        var refusal = members?.ReviewRefusal(workspaceId);
        if (refusal is not null)
            return new ForgeReviewOutcome(false, refusal);
        var submitted = await MutateAsync(
            workspaceId,
            async (forge, request) =>
            {
                await forge.SubmitReviewAsync(
                    request,
                    new ForgeSubmitReviewCommand(
                        command.ProposalNumber,
                        command.Body,
                        command.Event,
                        [.. (command.Findings ?? []).Select(finding => new ForgeReviewComment(finding.Path, finding.Line, finding.Body))],
                        command.CommitId),
                    cancellationToken);
                return true;
            },
            cancellationToken);
        return new ForgeReviewOutcome(submitted, null);
    }

    /// <summary>The account this workspace acts as on its host, which is how the host names that team member.</summary>
    public async Task<ForgeAccountDto?> ReadAccountAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var request = await RequestForAsync(workspaceId, cancellationToken);
        if (request is null)
            return null;
        var snapshot = await request.Value.Forge.ReadAsync(request.Value.Request, cancellationToken);
        return ToAccount(snapshot.Account);
    }

    /// <summary>
    /// Everything the host holds about the proposal from one task branch, read with this
    /// workspace's credential whatever the workspace has checked out.
    /// </summary>
    public async Task<ForgeTaskFeedbackDto?> ReadFeedbackAsync(string workspaceId, string branch, CancellationToken cancellationToken)
    {
        var request = await RequestForAsync(workspaceId, cancellationToken);
        if (request is null)
            return null;
        var snapshot = await request.Value.Forge.ReadAsync(request.Value.Request with { Branch = branch }, cancellationToken);
        return new ForgeTaskFeedbackDto(
            snapshot.Proposal is null ? null : ToProposal(snapshot.Proposal),
            snapshot.Proposal?.HeadSha,
            [.. snapshot.Checks.Select(ToCheck)],
            [.. snapshot.Threads.Select(ToThread)],
            [.. (snapshot.Reviews ?? []).Select(ToReview)]);
    }

    /// <summary>Gives an issue to the account that took it. False when the workspace has no usable forge.</summary>
    public async Task<bool> AssignIssueAsync(string workspaceId, int issueNumber, string login, CancellationToken cancellationToken)
        => await MutateAsync(
            workspaceId,
            async (forge, request) =>
            {
                await forge.AssignIssueAsync(request, issueNumber, login, cancellationToken);
                return true;
            },
            cancellationToken);

    /// <summary>Asks a named account to review a proposal, with this workspace's credential.</summary>
    public async Task<bool> RequestReviewAsync(string workspaceId, string proposalNumber, string login, CancellationToken cancellationToken)
        => await MutateAsync(
            workspaceId,
            async (forge, request) =>
            {
                await forge.RequestReviewAsync(request, proposalNumber, login, cancellationToken);
                return true;
            },
            cancellationToken);

    /// <summary>Stores a file on the host for a comment to link; null when the host has nowhere to put it.</summary>
    public async Task<string?> AttachAsync(
        string workspaceId,
        string number,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken)
        => (await MutateAsync(
            workspaceId,
            (forge, request) => forge.AttachAsync(request, number, fileName, contentType, content, cancellationToken),
            cancellationToken))?.Url;

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

    /// <summary>
    /// Pushes the workspace's HEAD to a task branch with its own credential. Only
    /// <see cref="TaskBranchPrefix"/> branches are written, so no base branch can ever be moved.
    /// </summary>
    public async Task<ForgeTransferOutcome> PushTaskBranchAsync(string workspaceId, string branch, CancellationToken cancellationToken)
    {
        if (!branch.StartsWith(TaskBranchPrefix, StringComparison.Ordinal) || branch.Length == TaskBranchPrefix.Length)
            return ForgeTransferOutcome.Refused($"Agent-Up pushes only {TaskBranchPrefix} branches.");
        return await TransferAsync(
            workspaceId,
            (path, remote, token) => transport!.PushAsync(path, remote, branch, token, cancellationToken),
            cancellationToken);
    }

    /// <summary>Fetches the named branches into the workspace's <c>origin</c> tracking refs.</summary>
    public Task<ForgeTransferOutcome> FetchBranchesAsync(string workspaceId, IReadOnlyList<string> branches, CancellationToken cancellationToken)
        => TransferAsync(
            workspaceId,
            (path, remote, token) => transport!.FetchAsync(path, remote, branches, token, cancellationToken),
            cancellationToken);

    private async Task<ForgeTransferOutcome> TransferAsync(
        string workspaceId,
        Func<string, string, string, Task> transfer,
        CancellationToken cancellationToken)
    {
        if (transport is null)
            return ForgeTransferOutcome.Refused("This Server has no Git transport for forge credentials.");
        var workspace = workspaces.GetById(workspaceId);
        var remote = workspace is null ? null : remotes.Resolve(workspace.WorktreePath);
        if (workspace is null || remote is null)
            return ForgeTransferOutcome.Refused("The workspace has no forge remote.");
        var request = await RequestForAsync(workspaceId, cancellationToken);
        if (request is null)
            return ForgeTransferOutcome.Refused("The workspace is not signed in to its forge.");
        try
        {
            await transfer(workspace.WorktreePath, remote.RemoteUrl, request.Value.Request.Token);
            return ForgeTransferOutcome.Done();
        }
        catch (InvalidOperationException exception)
        {
            return ForgeTransferOutcome.Refused(exception.Message);
        }
    }

    /// <summary>
    /// The repository this workspace's checkout points at. No credential is needed: the queue has
    /// to tell whose tasks a workspace may take even while the host is signed out.
    /// </summary>
    public ForgeRepositoryDto? ResolveRepository(string workspaceId)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        var remote = remotes.Resolve(workspace.WorktreePath);
        return remote is null ? null : new ForgeRepositoryDto(remote.Owner, remote.Repository);
    }

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

        // Reading the credential can go to the host: a GitHub App installation token is minted on
        // demand. A host that refuses or cannot be reached means this workspace has no usable
        // credential right now, which is the same answer as having none at all. Letting that throw
        // here would fail the whole read, because every caller guards only the call that follows -
        // so a reader asking for issues got a 500 rather than an empty list. What went wrong is
        // not lost: the hosting read reports it as unreachable, with the reason.
        string? token;
        try
        {
            token = await CredentialAsync(workspaceId, forge, remote, cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException
                                            or TaskCanceledException
                                            or InvalidOperationException)
        {
            return null;
        }

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
            : OAuthTokenAsync(workspaceId, forge, cancellationToken);

    /// <summary>
    /// The workspace's OAuth access token, refreshed first when it is about to expire. Refreshes
    /// for one workspace run one at a time, because hosts rotate the refresh token and a second
    /// refresh with the old one would be refused. A refresh the host refuses leaves the old token,
    /// which the host then answers with 401: the workspace reads as needing to sign in again.
    /// </summary>
    private async Task<string?> OAuthTokenAsync(string workspaceId, IForgeCapability forge, CancellationToken cancellationToken)
    {
        var grant = credentials.ReadGrant(workspaceId, forge.Identity.Id);
        var now = (time ?? TimeProvider.System).GetUtcNow();
        if (grant is null || !grant.NeedsRefresh(now))
            return grant?.AccessToken;

        var gate = _refreshes.GetOrAdd($"{workspaceId}/{forge.Identity.Id}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            grant = credentials.ReadGrant(workspaceId, forge.Identity.Id);
            if (grant is null || !grant.NeedsRefresh((time ?? TimeProvider.System).GetUtcNow()))
                return grant?.AccessToken;
            try
            {
                var refreshed = await exchange.RefreshAsync(grant.RefreshToken!, forge.Host.OAuthTokenUrl, cancellationToken);
                credentials.WriteGrant(workspaceId, forge.Identity.Id, refreshed);
                return refreshed.AccessToken;
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
            {
                return grant.AccessToken;
            }
        }
        finally
        {
            gate.Release();
        }
    }

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
        => new(review.Login, review.State, review.CommitId);

    private static ForgeAccountDto? ToAccount(ForgeAccount? account)
        => account is null ? null : new ForgeAccountDto(account.Login, account.DisplayName, account.IsBot, account.Url);

    private static ForgeProposalDto ToProposal(ForgeProposal proposal)
        => new(proposal.Id, proposal.Number, proposal.Title, proposal.Url, proposal.HeadRef, proposal.BaseRef, proposal.State);

    private static ForgeCheckDto ToCheck(ForgeCheck check)
        => new(check.Name, check.Status, check.Conclusion, check.Url);

    private static ForgeReviewThreadDto ToThread(ForgeReviewThread thread)
        => new(thread.Id, thread.Url, thread.Summary, thread.IsResolved, thread.Path, thread.Line, thread.Author);
}
