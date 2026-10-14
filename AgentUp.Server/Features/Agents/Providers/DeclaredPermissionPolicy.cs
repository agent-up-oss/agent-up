using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Matches an ACP permission request against declared workspace rules. An unmatched request
/// returns null so the loop can stop as <c>blocked:policy</c> rather than hanging or denying.
/// </summary>
public sealed class DeclaredPermissionPolicy(IReadOnlyList<PermissionRule> rules) : IAgentPermissionPolicy
{
    public IReadOnlyList<PermissionRule> Rules { get; } = rules;

    public PermissionDecision? Decide(JsonElement request)
    {
        var kind = Classify(request);
        var path = ReadPath(request);
        var command = ReadCommand(request);
        var rule = Rules.FirstOrDefault(item => Matches(item, kind, path, command));
        if (rule is null || !rule.Allow)
            return null;
        var option = PreferredAllowOption(request);
        return option is null ? null : new PermissionDecision(option, rule.Id);
    }

    internal static PermissionRuleKind Classify(JsonElement request)
    {
        var title = ReadTitle(request);
        var kind = ReadToolKind(request);
        if (kind.Contains("read", StringComparison.OrdinalIgnoreCase)
            || title.Contains("read", StringComparison.OrdinalIgnoreCase))
            return PermissionRuleKind.WorktreeRead;
        if (kind.Contains("network", StringComparison.OrdinalIgnoreCase)
            || title.Contains("fetch", StringComparison.OrdinalIgnoreCase)
            || title.Contains("http", StringComparison.OrdinalIgnoreCase))
            return PermissionRuleKind.Network;
        if (kind.Contains("execute", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("command", StringComparison.OrdinalIgnoreCase)
            || title.Contains("bash", StringComparison.OrdinalIgnoreCase)
            || title.Contains("shell", StringComparison.OrdinalIgnoreCase)
            || title.Contains("terminal", StringComparison.OrdinalIgnoreCase))
            return PermissionRuleKind.Execute;
        return PermissionRuleKind.WorktreeWrite;
    }

    private static bool Matches(PermissionRule rule, PermissionRuleKind kind, string path, string command)
    {
        if (rule.Kind != kind)
            return false;
        if (string.IsNullOrWhiteSpace(rule.Scope))
            return true;
        return kind switch
        {
            PermissionRuleKind.WorktreeWrite or PermissionRuleKind.WorktreeRead =>
                path.StartsWith(rule.Scope, StringComparison.Ordinal)
                || path.Contains(rule.Scope, StringComparison.Ordinal),
            PermissionRuleKind.Execute =>
                string.Equals(command, rule.Scope, StringComparison.Ordinal)
                || command.StartsWith(rule.Scope + " ", StringComparison.Ordinal),
            _ => true
        };
    }

    private static string PreferredAllowOption(JsonElement request)
    {
        if (!request.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
            return string.Empty;
        var ids = options.EnumerateArray()
            .Select(option => option.TryGetProperty("optionId", out var id) ? id.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToArray();
        return ids.FirstOrDefault(id => id.Contains("allow", StringComparison.OrdinalIgnoreCase))
               ?? ids.FirstOrDefault()
               ?? string.Empty;
    }

    private static string ReadTitle(JsonElement request)
    {
        if (request.TryGetProperty("toolCall", out var tool) && tool.TryGetProperty("title", out var title))
            return title.GetString() ?? "";
        return "";
    }

    private static string ReadToolKind(JsonElement request)
    {
        if (request.TryGetProperty("toolCall", out var tool) && tool.TryGetProperty("kind", out var kind))
            return kind.GetString() ?? "";
        return "";
    }

    private static string ReadPath(JsonElement request)
    {
        if (request.TryGetProperty("toolCall", out var tool)
            && tool.TryGetProperty("locations", out var locations)
            && locations.ValueKind == JsonValueKind.Array)
        {
            foreach (var location in locations.EnumerateArray())
            {
                if (location.ValueKind == JsonValueKind.String)
                    return location.GetString() ?? "";
                if (location.TryGetProperty("path", out var path))
                    return path.GetString() ?? "";
            }
        }

        return "";
    }

    private static string ReadCommand(JsonElement request)
    {
        if (request.TryGetProperty("toolCall", out var tool) && tool.TryGetProperty("command", out var command))
            return command.GetString() ?? "";
        return ReadTitle(request);
    }
}
