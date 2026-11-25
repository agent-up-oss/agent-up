using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;

namespace AgentUp.Server.Tests.Support;

/// <summary>No team, for a host that is not asking which reviews a repository owes.</summary>
internal sealed class EmptyTaskSourceTeam : ITaskSourceTeam
{
    public Task<TaskSourceTeam> ForAsync(string owner, string repository, CancellationToken cancellationToken)
        => Task.FromResult(new TaskSourceTeam(new Dictionary<string, string>(), []));
}

/// <summary>No team members, for a host that is not asking who can hold a token.</summary>
internal sealed class EmptyTaskSourceIdentities : ITaskSourceIdentities
{
    public IReadOnlyList<TaskSourcePerson> List() => [];
}

/// <summary>The named team members a test is saving tokens for.</summary>
internal sealed class FixedTaskSourceIdentities(params TaskSourcePerson[] people) : ITaskSourceIdentities
{
    public IReadOnlyList<TaskSourcePerson> List() => people;
}

/// <summary>The task source's tokens, held in memory.</summary>
internal sealed class MemoryTaskSourceCredentialStore : ITaskSourceCredentialStore
{
    private readonly Dictionary<string, TaskSourceCredential> _credentials = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Keys() => _credentials.Keys.ToArray();

    public TaskSourceCredential? Read(string key)
        => _credentials.TryGetValue(key, out var credential) ? credential : null;

    public void Write(string key, TaskSourceCredential credential) => _credentials[key] = credential;

    public void Delete(string key) => _credentials.Remove(key);
}
