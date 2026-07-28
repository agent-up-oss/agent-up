using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Features.Commits.DTOs;

public sealed record CommitsStatusResult(
    IReadOnlyList<CommitEntry> Entries,
    IReadOnlyList<string> UnassignedFiles,
    CommitsStatusSession? ActiveSession = null);
