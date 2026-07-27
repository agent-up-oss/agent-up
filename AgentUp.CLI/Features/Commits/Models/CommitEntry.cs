namespace AgentUp.CLI.Features.Commits.Models;

public sealed record CommitEntry(
    string Slice,
    string Message,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Tests,
    string Id = "",
    string PatchId = "")
{
    public string PatchKey => !string.IsNullOrWhiteSpace(PatchId)
        ? PatchId
        : string.IsNullOrWhiteSpace(Id) ? Slice : Id;
}
