using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Features.Commits.Providers;

internal sealed record CommitEntryJson(string Slice, string Message, List<string> Files, string? Id, string? PatchId, string? ReviewIssueId)
{
    public CommitEntry ToModel() => new(Slice, Message, Files, Id ?? "", PatchId ?? "", ReviewIssueId);
    public static CommitEntryJson FromModel(CommitEntry e) => new(e.Slice, e.Message, [.. e.Files], e.Id, e.PatchId, e.ReviewIssueId);
}
