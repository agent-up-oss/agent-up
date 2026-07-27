using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record CommitsStatusResult(
    IReadOnlyList<CommitEntry> Entries,
    IReadOnlyList<string> UnassignedFiles);
