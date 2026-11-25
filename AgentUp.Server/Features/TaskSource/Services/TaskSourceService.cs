using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Features.TaskSource.Services;

/// <summary>
/// The personal access tokens this Server uses to read every repository a host account can see.
///
/// One token per host, kept apart from the per-workspace forge credentials. A token is stored
/// only after the host confirms it can list repositories and read pull requests, because the
/// Tasks page lists those repositories and infers dev and PM review from the pull requests.
/// The token itself is never returned. The host URL is stored with it so the Server knows which
/// GitHub or Forgejo instance to call.
/// </summary>
public sealed class TaskSourceService(
    ITaskSourceCredentialStore credentials,
    IEnabledCapabilityPackages packages,
    ITaskSourceTeam team)
{
    private static readonly (string Id, string Name)[] Hosts = [("github", "GitHub"), ("forgejo", "Forgejo")];

    public TaskSourceDto Describe()
    {
        var enabled = packages.ListForges()
            .Select(forge => forge.Identity.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new TaskSourceDto(Hosts.Select(host => Module(host, enabled.Contains(host.Id))).ToArray());
    }

    /// <summary>
    /// Stores a token and host URL for one host, or refuses them. A refused token is not stored.
    /// </summary>
    public async Task<TaskSourceOutcome> SaveAsync(SaveTaskSourceTokenRequest request, CancellationToken cancellationToken)
    {
        var host = FindHost(request.ModuleId);
        if (host is null)
            return Refuse(StatusCodes.Status409Conflict, "Choose GitHub or Forgejo.");

        var token = request.Token.Trim();
        if (token.Length == 0)
            return Refuse(StatusCodes.Status409Conflict, "Paste a personal access token.");
        if (!TaskSourceEndpoint.TryNormalize(request.EndpointUrl, out var endpoint))
        {
            return Refuse(
                StatusCodes.Status409Conflict,
                string.IsNullOrWhiteSpace(request.EndpointUrl)
                    ? "Paste the host URL, such as https://github.com or your Forgejo origin."
                    : "The host URL must be an http or https address.");
        }

        var forge = FindForge(host.Value.Id);
        if (forge is null)
            return Refuse(StatusCodes.Status409Conflict, $"Enable the {host.Value.Name} capability, then paste the token.");

        ForgeTokenAccess access;
        try
        {
            access = await forge.ReadTokenAccessAsync(token, endpoint, cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            return Refuse(StatusCodes.Status503ServiceUnavailable, $"Could not reach {host.Value.Name} to check the token.");
        }

        if (!access.CanListRepositories || !access.CanReadPullRequests)
            return Refuse(StatusCodes.Status409Conflict, access.Detail ?? "The token needs access to repositories and pull requests.");

        credentials.Write(host.Value.Id, new TaskSourceCredential(token, endpoint));
        return new TaskSourceOutcome(StatusCodes.Status200OK, Describe());
    }

    /// <summary>Forgets one host, or every host when <paramref name="moduleId"/> is omitted.</summary>
    public TaskSourceDto Clear(string? moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            foreach (var host in Hosts)
                credentials.Delete(host.Id);
        }
        else
        {
            var host = FindHost(moduleId);
            if (host is not null)
                credentials.Delete(host.Value.Id);
        }

        return Describe();
    }

    public async Task<IReadOnlyList<TaskSourceRepositoryDto>> ListRepositoriesAsync(CancellationToken cancellationToken)
    {
        var listed = new List<TaskSourceRepositoryDto>();
        foreach (var host in Hosts)
        {
            var forge = FindForge(host.Id);
            var stored = credentials.Read(host.Id);
            if (forge is null || stored is null || string.IsNullOrWhiteSpace(stored.EndpointUrl))
                continue;

            IReadOnlyList<ForgeRepository> repositories;
            try
            {
                repositories = await forge.ListRepositoriesAsync(stored.Token, stored.EndpointUrl, cancellationToken);
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
            {
                repositories = [];
            }

            listed.AddRange(repositories.Select(repository => new TaskSourceRepositoryDto(host.Id, repository.Owner, repository.Name)));
        }

        return listed;
    }

    public async Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        var ready = Ready(moduleId, owner, repository);
        if (ready is null)
            return [];

        try
        {
            return await ready.Value.Forge.ListIssuesAsync(
                new ForgeRequest(ready.Value.Token, owner, repository, string.Empty, ready.Value.EndpointUrl),
                cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            return Array.Empty<ForgeIssue>();
        }
    }

    public async Task<IReadOnlyList<TaskSourcePullDto>> ListPullsAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        var ready = Ready(moduleId, owner, repository);
        if (ready is null)
            return [];

        IReadOnlyList<ForgePullRequest> pulls;
        try
        {
            pulls = await ready.Value.Forge.ListPullRequestsAsync(
                new ForgeRequest(ready.Value.Token, owner, repository, string.Empty, ready.Value.EndpointUrl),
                cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            pulls = [];
        }

        var members = await team.ForAsync(owner, repository, cancellationToken);
        return pulls.Select(pull => new TaskSourcePullDto(
            pull.Id,
            pull.Number,
            pull.Title,
            pull.Url,
            PullRequestReviewInference.For(pull, members.KindByLogin, members.Kinds)
                .Select(review => new TaskSourceReviewDto(review.Kind, review.Standing, review.Login, review.Verdict))
                .ToArray())).ToArray();
    }

    private TaskSourceModuleDto Module((string Id, string Name) host, bool enabled)
    {
        var stored = credentials.Read(host.Id);
        var token = stored is not null && !string.IsNullOrWhiteSpace(stored.Token);
        var endpoint = stored is not null && !string.IsNullOrWhiteSpace(stored.EndpointUrl) ? stored.EndpointUrl : null;
        var connected = token && endpoint is not null && enabled;
        return new TaskSourceModuleDto(
            host.Id,
            host.Name,
            connected,
            Detail(host.Name, connected, enabled, token, endpoint),
            endpoint);
    }

    private static string Detail(string name, bool connected, bool enabled, bool stored, string? endpoint)
    {
        if (!enabled && !stored)
            return $"Enable the {name} capability, then paste the host URL and a personal access token.";
        if (!enabled)
            return $"Enable the {name} capability to use the saved token.";
        if (connected)
            return $"Reading repositories and pull requests from {name} at {endpoint}.";
        return "Paste the host URL and a personal access token. It needs access to repositories and pull requests, so Agent-Up can list every repository and read which reviews each pull request still needs.";
    }

    private (IForgeCapability Forge, string Token, string EndpointUrl)? Ready(string moduleId, string owner, string repository)
    {
        var host = FindHost(moduleId);
        if (host is null || string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository))
            return null;
        var forge = FindForge(host.Value.Id);
        var stored = credentials.Read(host.Value.Id);
        return forge is null
            || stored is null
            || string.IsNullOrWhiteSpace(stored.Token)
            || string.IsNullOrWhiteSpace(stored.EndpointUrl)
            ? null
            : (forge, stored.Token, stored.EndpointUrl);
    }

    private IForgeCapability? FindForge(string moduleId) =>
        packages.ListForges().FirstOrDefault(forge => forge.Identity.Id.Equals(moduleId, StringComparison.OrdinalIgnoreCase));

    private static (string Id, string Name)? FindHost(string? moduleId) =>
        Hosts.Cast<(string Id, string Name)?>().FirstOrDefault(host =>
            host!.Value.Id.Equals(moduleId?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static TaskSourceOutcome Refuse(int status, string detail) =>
        new(status, new { title = "Task source", detail });
}

/// <summary>A status and a body, so the controller stays a pass-through.</summary>
public sealed record TaskSourceOutcome(int StatusCode, object? Body);
