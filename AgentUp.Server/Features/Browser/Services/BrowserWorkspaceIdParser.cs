namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserWorkspaceIdParser
{
    public IReadOnlyList<string> Parse(string? workspaceIds) =>
        workspaceIds?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList() ?? [];
}
