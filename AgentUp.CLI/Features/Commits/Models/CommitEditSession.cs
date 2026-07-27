namespace AgentUp.CLI.Features.Commits.Models;

public sealed record CommitEditSession(
    string EntryId,
    string OriginalPatchKey,
    IReadOnlyList<string> Files);
