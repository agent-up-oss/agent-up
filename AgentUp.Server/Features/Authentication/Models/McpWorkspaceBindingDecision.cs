using System.Text.Json;

namespace AgentUp.Server.Features.Authentication.Models;

public sealed record McpWorkspaceBindingDecision(
    bool Allowed,
    string? Error,
    IReadOnlyDictionary<string, JsonElement> Arguments)
{
    public static McpWorkspaceBindingDecision Allow(IReadOnlyDictionary<string, JsonElement> arguments)
        => new(true, null, arguments);

    public static McpWorkspaceBindingDecision Forbid(string error)
        => new(false, error, new Dictionary<string, JsonElement>());
}
