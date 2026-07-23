namespace AgentUp.Desktop.Features.FirstRun.DTOs;

internal sealed record AgentUpCommand(string FileName, string ArgumentsPrefix, bool IsFallback);
