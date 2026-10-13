using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Allows or rejects every permission request with one recorded rule. Unmatched option
/// ids return null so the loop can stop as <c>blocked:policy</c>.
/// </summary>
public sealed class AllowListedPermissionPolicy(string optionId, string ruleId) : IAgentPermissionPolicy
{
    public PermissionDecision? Decide(JsonElement request)
    {
        if (!request.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
            return null;

        var ids = options.EnumerateArray()
            .Select(option => option.TryGetProperty("optionId", out var id) ? id.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id));
        return ids.Contains(optionId, StringComparer.Ordinal)
            ? new PermissionDecision(optionId, ruleId)
            : null;
    }
}
