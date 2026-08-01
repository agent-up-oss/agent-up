namespace AgentUp.Server.Features.Browser.Models;

public sealed record BrowserPendingCommandResult(
    bool HasWorkspaceIds,
    BrowserCommandDto? Command);
