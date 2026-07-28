using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Features.Commits.DTOs;

public sealed record CommitInspectResult(CommitEntry Entry, string? Patch);
