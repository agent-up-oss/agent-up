namespace AgentUp.Server.Features.Forge.Interfaces;

/// <summary>
/// Moves Git objects between a workspace's clone and its host with that workspace's own forge
/// credential, so a team member's pushes and fetches are attributed to its account and never rely
/// on whatever credential the Server's user happens to have.
/// </summary>
public interface IForgeGitTransport
{
    Task PushAsync(string repositoryPath, string remoteUrl, string branch, string token, CancellationToken cancellationToken);

    /// <summary>Fetches each branch into <c>refs/remotes/origin/&lt;branch&gt;</c>.</summary>
    Task FetchAsync(string repositoryPath, string remoteUrl, IReadOnlyList<string> branches, string token, CancellationToken cancellationToken);
}
