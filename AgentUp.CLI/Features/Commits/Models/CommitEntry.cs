namespace AgentUp.CLI.Features.Commits.Models;

public sealed record CommitEntry(
    string Slice,
    string Message,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Tests);
