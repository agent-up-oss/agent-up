using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.Providers;

internal sealed record CommitEntryJson(string Slice, string Message, List<string> Files, List<string> Tests)
{
    public CommitEntry ToModel() => new(Slice, Message, Files, Tests);
    public static CommitEntryJson FromModel(CommitEntry e) => new(e.Slice, e.Message, [.. e.Files], [.. e.Tests]);
}
