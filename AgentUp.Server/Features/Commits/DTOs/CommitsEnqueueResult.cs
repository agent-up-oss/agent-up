namespace AgentUp.Server.Features.Commits.DTOs;

public sealed record CommitsEnqueueResult(bool Succeeded, string Message, int QueueSize = 0, string? QueueWorktreePath = null, long Generation = 0);
