using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.Providers;

internal sealed record CommitsQueueJson(int Version, List<CommitEntryJson> Commits)
{
    public CommitsQueue ToModel() => new(Version, Commits.Select(e => e.ToModel()).ToList());
    public static CommitsQueueJson FromModel(CommitsQueue q) => new(q.Version, q.Commits.Select(CommitEntryJson.FromModel).ToList());
}
