using System.Text.Json;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Models;

namespace AgentUp.Server.Features.Authentication.Services;

public sealed class McpWorkspaceArgumentBinder(IBoundWorkspaceCatalog catalog)
{
    public async Task<McpWorkspaceBindingDecision> BindAsync(
        string boundWorkspace,
        IReadOnlyDictionary<string, JsonElement> arguments,
        IReadOnlyList<string> workspaceIdParameters,
        CancellationToken cancellationToken)
    {
        var rewritten = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in arguments)
            rewritten[key] = value;

        foreach (var (key, value) in arguments)
        {
            var text = ReadArgumentText(value);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (IsWorkspaceIdParameter(key, workspaceIdParameters))
            {
                if (!string.Equals(text, boundWorkspace, StringComparison.Ordinal))
                    return McpWorkspaceBindingDecision.Forbid(Refusal(boundWorkspace, text));
                continue;
            }

            if (!McpWorkspaceTargetArguments.IsPathName(key))
                continue;

            if (!await catalog.PathTargetsWorkspaceAsync(boundWorkspace, text, cancellationToken))
                return McpWorkspaceBindingDecision.Forbid(Refusal(boundWorkspace, text));
        }

        foreach (var name in workspaceIdParameters.Where(name => !HasText(rewritten, name)))
            rewritten[name] = JsonSerializer.SerializeToElement(boundWorkspace);

        return McpWorkspaceBindingDecision.Allow(rewritten);
    }

    private static bool IsWorkspaceIdParameter(string key, IReadOnlyList<string> workspaceIdParameters)
        => McpWorkspaceTargetArguments.IsAlwaysBoundName(key) && !McpWorkspaceTargetArguments.IsPathName(key)
           || workspaceIdParameters.Contains(key, StringComparer.OrdinalIgnoreCase);

    private static bool HasText(IReadOnlyDictionary<string, JsonElement> arguments, string name)
        => arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(ReadArgumentText(value));

    private static string? ReadArgumentText(JsonElement value)
        => value.ValueKind is JsonValueKind.String ? value.GetString() : value.GetRawText();

    private static string Refusal(string boundWorkspace, string target)
        => $"This token is bound to workspace '{boundWorkspace}' and cannot target '{target}'.";
}
