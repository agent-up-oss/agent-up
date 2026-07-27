using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record CommitInspectResult(CommitEntry Entry, string? Patch);
