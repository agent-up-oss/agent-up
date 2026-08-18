namespace AgentUp.Browser.Streaming.Models;

public sealed record BrowserPendingCommandResult(
    bool HasWorkspaceIds,
    BrowserCommandDto? Command);
