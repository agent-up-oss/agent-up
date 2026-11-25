using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Features.TaskSource.Services;

/// <summary>
/// The personal access tokens this Server uses to read every repository the team can see.
///
/// One token per team member, kept apart from the per-workspace forge credentials those
/// members use to push and review. A token is stored only after the host confirms it can list
/// repositories and read pull requests, because the Tasks page lists those repositories and
/// infers dev and PM review from the pull requests. The token itself is never returned. The
/// host URL is stored with it so the Server knows which GitHub or Forgejo instance to call.
/// The instance unions every member's repositories and names who can see each one.
/// </summary>
public sealed class TaskSourceService(
    ITaskSourceCredentialStore credentials,
    IEnabledCapabilityPackages packages,
    ITaskSourceTeam team,
    ITaskSourceIdentities identities)
{
    private static readonly (string Id, string Name)[] Hosts = [("github", "GitHub"), ("forgejo", "Forgejo")];

    public TaskSourceDto Describe()
    {
        var enabled = packages.ListForges()
            .Select(forge => forge.Identity.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new TaskSourceDto(identities.List().Select(person => Identity(person, enabled)).ToArray());
    }

    /// <summary>
    /// Stores a token and host URL for one team member, or refuses them. A refused token is not
    /// stored.
    /// </summary>
    public async Task<TaskSourceOutcome> SaveAsync(SaveTaskSourceTokenRequest request, CancellationToken cancellationToken)
    {
        var person = FindPerson(request.WorkspaceId);
        if (person is null)
            return Refuse(StatusCodes.Status409Conflict, "Choose a team member.");

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

        credentials.Write(person.WorkspaceId, new TaskSourceCredential(token, endpoint, host.Value.Id));
        return new TaskSourceOutcome(StatusCodes.Status200OK, Describe());
    }

    /// <summary>Forgets one member, or every member when <paramref name="workspaceId"/> is omitted.</summary>
    public TaskSourceDto Clear(string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            foreach (var key in credentials.Keys())
                credentials.Delete(key);
        }
        else
        {
            credentials.Delete(workspaceId.Trim());
        }

        return Describe();
    }

    public async Task<IReadOnlyList<TaskSourceRepositoryDto>> ListRepositoriesAsync(CancellationToken cancellationToken)
    {
        var listed = new Dictionary<string, TaskSourceRepositoryDto>(StringComparer.OrdinalIgnoreCase);
        var people = identities.List().ToDictionary(person => person.WorkspaceId, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Entries())
        {
            var forge = FindForge(entry.ModuleId);
            if (forge is null)
                continue;

            IReadOnlyList<ForgeRepository> repositories;
            try
            {
                repositories = await forge.ListRepositoriesAsync(entry.Token, entry.EndpointUrl, cancellationToken);
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
            {
                repositories = [];
            }

            people.TryGetValue(entry.Key, out var person);
            foreach (var repository in repositories)
            {
                var id = $"{entry.ModuleId}/{repository.Owner}/{repository.Name}";
                if (!listed.TryGetValue(id, out var current))
                {
                    current = new TaskSourceRepositoryDto(entry.ModuleId, repository.Owner, repository.Name, []);
                    listed[id] = current;
                }

                if (person is null)
                    continue;
                var members = current.Members?.ToList() ?? [];
                if (members.Any(member => member.WorkspaceId.Equals(person.WorkspaceId, StringComparison.OrdinalIgnoreCase)))
                    continue;
                members.Add(new TaskSourceMemberDto(person.WorkspaceId, person.DisplayName, MemberInitials.From(person.DisplayName)));
                listed[id] = current with { Members = members };
            }
        }

        return listed.Values.ToArray();
    }

    public async Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        var seen = new Dictionary<string, ForgeIssue>(StringComparer.Ordinal);
        foreach (var ready in ReadyAll(moduleId))
        {
            IReadOnlyList<ForgeIssue> issues;
            try
            {
                issues = await ready.Forge.ListIssuesAsync(
                    new ForgeRequest(ready.Token, owner, repository, string.Empty, ready.EndpointUrl),
                    cancellationToken);
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
            {
                issues = [];
            }

            foreach (var issue in issues)
                seen[issue.Id] = issue;
        }

        return seen.Values.ToArray();
    }

    public async Task<IReadOnlyList<TaskSourcePullDto>> ListPullsAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        var seen = new Dictionary<string, ForgePullRequest>(StringComparer.Ordinal);
        foreach (var ready in ReadyAll(moduleId))
        {
            IReadOnlyList<ForgePullRequest> pulls;
            try
            {
                pulls = await ready.Forge.ListPullRequestsAsync(
                    new ForgeRequest(ready.Token, owner, repository, string.Empty, ready.EndpointUrl),
                    cancellationToken);
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
            {
                pulls = [];
            }

            foreach (var pull in pulls)
                seen[pull.Id] = pull;
        }

        var members = await team.ForAsync(owner, repository, cancellationToken);
        return seen.Values.Select(pull => new TaskSourcePullDto(
            pull.Id,
            pull.Number,
            pull.Title,
            pull.Url,
            PullRequestReviewInference.For(pull, members.KindByLogin, members.Kinds)
                .Select(review => new TaskSourceReviewDto(review.Kind, review.Standing, review.Login, review.Verdict))
                .ToArray())).ToArray();
    }

    private TaskSourceIdentityDto Identity(TaskSourcePerson person, HashSet<string> enabled)
    {
        var stored = credentials.Read(person.WorkspaceId);
        var moduleId = stored is { ModuleId.Length: > 0 } ? stored.ModuleId : null;
        var token = stored is not null && !string.IsNullOrWhiteSpace(stored.Token);
        var endpoint = stored is not null && !string.IsNullOrWhiteSpace(stored.EndpointUrl) ? stored.EndpointUrl : null;
        var host = FindHost(moduleId);
        var name = host?.Name ?? "GitHub";
        var hostEnabled = moduleId is not null && enabled.Contains(moduleId);
        var connected = token && endpoint is not null && hostEnabled;
        return new TaskSourceIdentityDto(
            person.WorkspaceId,
            person.DisplayName,
            MemberInitials.From(person.DisplayName),
            token ? moduleId : null,
            connected,
            Detail(name, person.DisplayName, connected, hostEnabled || moduleId is null, token, endpoint),
            token ? endpoint : null);
    }

    private static string Detail(string name, string member, bool connected, bool enabled, bool stored, string? endpoint)
    {
        if (!enabled && !stored)
            return $"Enable the {name} capability, then paste the host URL and a personal access token for {member}.";
        if (!enabled)
            return $"Enable the {name} capability to use {member}'s saved token.";
        if (connected)
            return $"Reading repositories and pull requests as {member} from {name} at {endpoint}.";
        return $"Paste the host URL and a personal access token for {member}. It needs access to repositories and pull requests, so Agent-Up can list every repository this team member can see.";
    }

    private IEnumerable<(string Key, string ModuleId, string Token, string EndpointUrl)> Entries()
    {
        foreach (var key in credentials.Keys())
        {
            var stored = credentials.Read(key);
            if (stored is null || string.IsNullOrWhiteSpace(stored.Token) || string.IsNullOrWhiteSpace(stored.EndpointUrl))
                continue;
            var moduleId = stored.ModuleId.Length > 0 ? stored.ModuleId : HostKey(key);
            if (moduleId.Length == 0)
                continue;
            yield return (key, moduleId, stored.Token, stored.EndpointUrl);
        }
    }

    private IEnumerable<(IForgeCapability Forge, string Token, string EndpointUrl)> ReadyAll(string moduleId)
    {
        var host = FindHost(moduleId);
        if (host is null)
            yield break;
        var forge = FindForge(host.Value.Id);
        if (forge is null)
            yield break;
        foreach (var entry in Entries().Where(entry =>
            entry.ModuleId.Equals(host.Value.Id, StringComparison.OrdinalIgnoreCase)))
        {
            yield return (forge, entry.Token, entry.EndpointUrl);
        }
    }

    private TaskSourcePerson? FindPerson(string? workspaceId)
        => identities.List().FirstOrDefault(person =>
            person.WorkspaceId.Equals(workspaceId?.Trim(), StringComparison.OrdinalIgnoreCase));

    private IForgeCapability? FindForge(string moduleId) =>
        packages.ListForges().FirstOrDefault(forge => forge.Identity.Id.Equals(moduleId, StringComparison.OrdinalIgnoreCase));

    private static (string Id, string Name)? FindHost(string? moduleId) =>
        Hosts.Cast<(string Id, string Name)?>().FirstOrDefault(host =>
            host!.Value.Id.Equals(moduleId?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string HostKey(string key)
        => key.Equals("github", StringComparison.OrdinalIgnoreCase)
            || key.Equals("forgejo", StringComparison.OrdinalIgnoreCase)
            ? key.ToLowerInvariant()
            : "";

    private static TaskSourceOutcome Refuse(int status, string detail) =>
        new(status, new { title = "Task source", detail });
}

/// <summary>A status and a body, so the controller stays a pass-through.</summary>
public sealed record TaskSourceOutcome(int StatusCode, object? Body);
