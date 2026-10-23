using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Capabilities.Interfaces;
using Microsoft.AspNetCore.Http;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Forge.Services;

public sealed class ForgeService(
    WorkspaceQueryController workspaces,
    IEnabledCapabilityPackages packages,
    IForgeCredentialStore credentials,
    IForgeGitRemoteResolver remotes,
    IForgeOAuthSettings oauth,
    IForgeOAuthStateStore states,
    IForgeOAuthExchange exchange)
{
    public async Task<ForgeHostingDto?> GetHostingAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        var forge = packages.ListForges().FirstOrDefault();
        if (forge is null)
            return None();

        var token = credentials.Read(workspaceId, forge.Identity.Id);
        if (string.IsNullOrWhiteSpace(token))
            return new ForgeHostingDto("needsCredential", forge.Identity.Id, null, [], [], null, null);

        var remote = remotes.Resolve(workspace.WorktreePath);
        if (remote is null)
            return new ForgeHostingDto("unreachable", forge.Identity.Id, null, [], [], null, "No GitHub origin is configured.");

        try
        {
            var snapshot = await forge.ReadAsync(
                new ForgeRequest(token, remote.Owner, remote.Repository, remote.Branch),
                cancellationToken);
            var account = ToAccount(snapshot.Account);
            if (snapshot.Proposal is null)
                return new ForgeHostingDto("noProposal", forge.Identity.Id, null, [], [], null, null, account);

            return new ForgeHostingDto(
                "ready",
                forge.Identity.Id,
                ToProposal(snapshot.Proposal),
                [.. snapshot.Checks.Select(ToCheck)],
                [.. snapshot.Threads.Select(ToThread)],
                snapshot.Mergeability is null ? null : new ForgeMergeabilityDto(snapshot.Mergeability.Mergeable, snapshot.Mergeability.State),
                null,
                account,
                [.. (snapshot.Reviews ?? []).Select(ToReview)]);
        }
        catch (HttpRequestException exception)
        {
            return new ForgeHostingDto("unreachable", forge.Identity.Id, null, [], [], null, exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new ForgeHostingDto("unreachable", forge.Identity.Id, null, [], [], null, exception.Message);
        }
    }

    public ForgeHttpOutcome StartLogin(string workspaceId)
    {
        if (workspaces.GetById(workspaceId) is null)
            return new ForgeHttpOutcome(StatusCodes.Status404NotFound, null);

        var configuration = oauth.Read();
        if (!configuration.IsConfigured)
            return new ForgeHttpOutcome(
                StatusCodes.Status503ServiceUnavailable,
                new { title = "OAuth unavailable", detail = "GitHub OAuth is not configured." });

        var state = states.Issue(workspaceId);
        var url =
            "https://github.com/login/oauth/authorize"
            + $"?client_id={Uri.EscapeDataString(configuration.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(configuration.RedirectUri)}"
            + $"&state={Uri.EscapeDataString(state)}"
            + "&scope=repo";
        return new ForgeHttpOutcome(StatusCodes.Status200OK, new ForgeLoginStartDto(url));
    }

    public async Task<bool> CompleteLoginAsync(string code, string state, CancellationToken cancellationToken)
    {
        var workspaceId = states.Consume(state);
        if (workspaceId is null || workspaces.GetById(workspaceId) is null)
            return false;

        var forge = packages.ListForges().FirstOrDefault();
        if (forge is null)
            return false;

        credentials.Write(workspaceId, forge.Identity.Id, await exchange.ExchangeAsync(code, cancellationToken));
        return true;
    }

    public bool Logout(string workspaceId)
    {
        if (workspaces.GetById(workspaceId) is null)
            return false;

        var forge = packages.ListForges().FirstOrDefault();
        if (forge is not null)
            credentials.Delete(workspaceId, forge.Identity.Id);
        return true;
    }

    public async Task<IReadOnlyList<ForgeAssignedIssue>> ListAssignedAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var request = RequestFor(workspaceId);
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

    public Task<ForgeProposal?> OpenProposalAsync(
        string workspaceId,
        ForgeOpenProposalRequest command,
        CancellationToken cancellationToken)
        => MutateAsync(
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
        var request = RequestFor(workspaceId);
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

    public Task CommentAsync(string workspaceId, ForgeCommentRequest command, CancellationToken cancellationToken)
        => MutateAsync(
            workspaceId,
            (forge, request) => forge.CommentAsync(request, new ForgeCommentCommand(command.ProposalNumber, command.Body), cancellationToken),
            cancellationToken);

    public Task SubmitReviewAsync(string workspaceId, ForgeReviewRequest command, CancellationToken cancellationToken)
        => MutateAsync(
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
        var request = RequestFor(workspaceId);
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

    private (IForgeCapability Forge, ForgeRequest Request)? RequestFor(string workspaceId)
    {
        var workspace = workspaces.GetById(workspaceId);
        var forge = packages.ListForges().FirstOrDefault();
        if (workspace is null || forge is null)
            return null;

        var token = credentials.Read(workspaceId, forge.Identity.Id);
        var remote = remotes.Resolve(workspace.WorktreePath);
        if (string.IsNullOrWhiteSpace(token) || remote is null)
            return null;

        return (forge, new ForgeRequest(token, remote.Owner, remote.Repository, remote.Branch));
    }

    private static ForgeHostingDto None()
        => new("none", null, null, [], [], null, null);

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
