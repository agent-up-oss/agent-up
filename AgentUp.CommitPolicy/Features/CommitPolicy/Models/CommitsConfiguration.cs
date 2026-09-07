namespace AgentUp.CommitPolicy.Features.CommitPolicy.Models;

public sealed record CommitsProjectConfiguration(
    IReadOnlyList<string> Test,
    IReadOnlyList<string> DependsOn);

public sealed record CommitsConfiguration(
    IReadOnlyList<string> Build,
    IReadOnlyList<string> Test,
    IReadOnlyDictionary<string, CommitsProjectConfiguration> Projects)
{
    public static readonly CommitsConfiguration Empty = new([], [], new Dictionary<string, CommitsProjectConfiguration>());
}
