namespace AgentUp.CLI.Features.Commits.Models;

public sealed record CommitsQueue(int Version, IReadOnlyList<CommitEntry> Commits)
{
    public static CommitsQueue Empty() => new(1, []);
}
