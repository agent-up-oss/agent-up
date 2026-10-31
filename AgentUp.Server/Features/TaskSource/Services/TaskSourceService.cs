using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Features.TaskSource.Services;

/// <summary>
/// The repository this Server's agents take work from, and the sign-in that lets it be read.
///
/// The source is named once, for the Server, instead of being inferred from whichever workspace a
/// reader happened to open. The sign-in is the same OAuth the forge slice already performs - the
/// same settings, the same code exchange - run for the Server rather than for one workspace, so
/// there is one flow to get right rather than two that drift apart.
/// </summary>
public sealed class TaskSourceService(
    ITaskSourceStore store,
    ITaskSourceCredentialStore credentials,
    ITaskSourceStateStore states,
    IEnabledCapabilityPackages packages,
    IForgeOAuthSettings oauth,
    IForgeOAuthExchange exchange)
{
    public TaskSourceSetting? Read() => store.Read();

    /// <summary>What the clients draw: the source, whether it can be read, and what is missing.</summary>
    public TaskSourceDto Describe()
    {
        var source = store.Read();
        var modules = packages.ListForges()
            .Select(forge => new TaskSourceModuleDto(forge.Identity.Id, forge.Identity.DisplayName))
            .ToArray();
        var configured = source is { IsComplete: true };
        var connected = configured && !string.IsNullOrWhiteSpace(credentials.Read(source!.ModuleId));
        var forge = configured ? Find(source!.ModuleId) : null;

        return new TaskSourceDto(
            source?.ModuleId,
            source?.Owner,
            source?.Repository,
            configured,
            connected,
            CanConnect(forge),
            Detail(source, forge, connected, modules.Length),
            modules);
    }

    /// <summary>
    /// Points the Server at a repository. Changing where the work comes from drops the token for
    /// the module it was pointed at before, because a credential is for one host and keeping it
    /// would leave the Server holding an authorization nobody asked it to keep.
    /// </summary>
    public TaskSourceDto Set(SetTaskSourceRequest request)
    {
        var existing = store.Read();
        var source = new TaskSourceSetting(
            request.ModuleId.Trim(),
            request.Owner.Trim(),
            request.Repository.Trim());

        if (existing is not null && !existing.ModuleId.Equals(source.ModuleId, StringComparison.OrdinalIgnoreCase))
            credentials.Delete(existing.ModuleId);

        store.Write(source.IsComplete ? source : null);
        return Describe();
    }

    /// <summary>Forgets the source and the token that went with it.</summary>
    public TaskSourceDto Clear()
    {
        var existing = store.Read();
        if (existing is not null) credentials.Delete(existing.ModuleId);
        store.Write(null);
        return Describe();
    }

    /// <summary>
    /// Where to send the reader to authorize.
    /// </summary>
    public TaskSourceOutcome StartLogin()
    {
        var source = store.Read();
        if (source is not { IsComplete: true })
            return new TaskSourceOutcome(
                StatusCodes.Status409Conflict,
                new { title = "No task source", detail = "Name the repository the agents work from before signing in." });

        var forge = Find(source.ModuleId);
        if (forge is null)
            return new TaskSourceOutcome(
                StatusCodes.Status503ServiceUnavailable,
                new { title = "Host not installed", detail = $"The '{source.ModuleId}' module is not enabled on this Server." });

        var configuration = oauth.Read();
        var authorizeUrl = forge.Host.OAuthAuthorizeUrl;
        if (!configuration.IsConfigured || string.IsNullOrWhiteSpace(authorizeUrl))
            return new TaskSourceOutcome(
                StatusCodes.Status503ServiceUnavailable,
                new { title = "Sign-in is not configured", detail = OAuthMissingDetail });

        var state = states.Issue(source.ModuleId);
        var separator = authorizeUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var url = authorizeUrl
                  + separator
                  + "response_type=code"
                  + $"&client_id={Uri.EscapeDataString(configuration.ClientId)}"
                  + $"&redirect_uri={Uri.EscapeDataString(configuration.RedirectUri)}"
                  + $"&state={Uri.EscapeDataString(state)}"
                  + "&scope=repo";
        return new TaskSourceOutcome(StatusCodes.Status200OK, new TaskSourceLoginDto(url));
    }

    /// <summary>
    /// Finishes the sign-in the redirect began.
    ///
    /// The state is spent here, so a callback replayed with the same one is refused rather than
    /// exchanged twice. A code that the host will not exchange leaves the source unconnected
    /// rather than failing the callback with a stack trace in the reader's browser.
    /// </summary>
    public async Task<bool> CompleteLoginAsync(string code, string state, CancellationToken cancellationToken)
    {
        var moduleId = states.Consume(state);
        if (moduleId is null) return false;

        var forge = Find(moduleId);
        if (forge is null) return false;

        try
        {
            var token = await exchange.ExchangeAsync(code, forge.Host.OAuthTokenUrl, cancellationToken);
            if (string.IsNullOrWhiteSpace(token)) return false;
            credentials.Write(moduleId, token);
            return true;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// The source's open issues, or none where it is not named or not signed in.
    ///
    /// A host that cannot be reached answers with nothing rather than throwing: the surface that
    /// reports why is the source itself, which says whether it is connected.
    /// </summary>
    public async Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(CancellationToken cancellationToken)
    {
        var source = store.Read();
        if (source is not { IsComplete: true }) return [];

        var forge = Find(source.ModuleId);
        if (forge is null) return [];

        var token = credentials.Read(source.ModuleId);
        if (string.IsNullOrWhiteSpace(token)) return [];

        try
        {
            // A branch is part of a forge request but means nothing to a repository's issue list,
            // which belongs to the repository rather than to any one branch.
            return await forge.ListIssuesAsync(
                new ForgeRequest(token, source.Owner, source.Repository, string.Empty),
                cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            return [];
        }
    }

    private IForgeCapability? Find(string moduleId)
        => packages.ListForges()
            .FirstOrDefault(forge => forge.Identity.Id.Equals(moduleId, StringComparison.OrdinalIgnoreCase));

    private bool CanConnect(IForgeCapability? forge)
        => forge is not null
           && oauth.Read().IsConfigured
           && !string.IsNullOrWhiteSpace(forge.Host.OAuthAuthorizeUrl);

    private string Detail(TaskSourceSetting? source, IForgeCapability? forge, bool connected, int moduleCount)
    {
        if (moduleCount == 0)
            return "No repository host is enabled on this Server. Enable one under capabilities first.";
        if (source is not { IsComplete: true })
            return "Name the repository the agents take their work from.";
        if (forge is null)
            return $"The '{source.ModuleId}' module is not enabled on this Server.";
        if (connected)
            return $"Reading issues from {source.RepositoryKey}.";
        return CanConnect(forge)
            ? $"Sign in to read issues from {source.RepositoryKey}."
            : OAuthMissingDetail;
    }

    private const string OAuthMissingDetail =
        "This Server has no OAuth client configured for that host, so it cannot start a sign-in.";
}

/// <summary>A status and a body, so the controller stays a pass-through.</summary>
public sealed record TaskSourceOutcome(int StatusCode, object? Body);
