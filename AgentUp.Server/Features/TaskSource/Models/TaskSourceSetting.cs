namespace AgentUp.Server.Features.TaskSource.Models;

/// <summary>
/// The repository the agents' work comes from.
///
/// It belongs to the Server rather than to a workspace. Before this, the issues a reader could
/// pick from were whatever the opened workspace's git remote happened to point at, which made the
/// source implicit, unconfigurable, and different depending on which agent you were looking at.
/// One Server works one backlog, so it says which one.
/// </summary>
public sealed record TaskSourceSetting(string ModuleId, string Owner, string Repository)
{
    public string RepositoryKey => $"{Owner}/{Repository}";

    /// <summary>True once it names a repository that could be read.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ModuleId)
        && !string.IsNullOrWhiteSpace(Owner)
        && !string.IsNullOrWhiteSpace(Repository);
}
