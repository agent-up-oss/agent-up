using AgentUp.Server.Features.Forge.Interfaces;

namespace AgentUp.Server.Tests.Fake;

/// <summary>Records the pushes and fetches the Server makes with a workspace's forge credential.</summary>
internal sealed class RecordingForgeGitTransport : IForgeGitTransport
{
    public List<(string Branch, string Token)> Pushed { get; } = [];

    public List<IReadOnlyList<string>> Fetched { get; } = [];

    public string? PushFailure { get; set; }

    public Task PushAsync(string repositoryPath, string remoteUrl, string branch, string token, CancellationToken cancellationToken)
    {
        if (PushFailure is not null)
            return Task.FromException(new InvalidOperationException(PushFailure));
        Pushed.Add((branch, token));
        return Task.CompletedTask;
    }

    public Task FetchAsync(string repositoryPath, string remoteUrl, IReadOnlyList<string> branches, string token, CancellationToken cancellationToken)
    {
        Fetched.Add(branches);
        return Task.CompletedTask;
    }
}
