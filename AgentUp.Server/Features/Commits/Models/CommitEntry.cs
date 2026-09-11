namespace AgentUp.Server.Features.Commits.Models;

public sealed record CommitEntry(
    string Slice,
    string Message,
    IReadOnlyList<string> Files,
    string Id = "",
    string PatchId = "",
    string? ReviewIssueId = null)
{
    public string PatchKey => !string.IsNullOrWhiteSpace(PatchId)
        ? PatchId
        : string.IsNullOrWhiteSpace(Id) ? Slice : Id;
}
