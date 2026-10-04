using System.Text.Json;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Models;

namespace AgentUp.Server.Features.Authentication.Services;

/// <summary>
/// Pins an MCP tool call to the workspace its token names.
/// </summary>
/// <remarks>
/// A bound session is never shown a workspace target in the advertised schema, so it
/// supplies none and this fills them in: the bound id for every id parameter, and - only
/// when a tool identifies its target by path alone - that workspace's worktree path.
/// <para>
/// A tool that takes both is pinned by the id, and any path target the caller sent anyway
/// is dropped once it has been checked against the bound workspace. Leaving it in place
/// would reach the tool as two targets at once, which every target-taking tool refuses,
/// and keeping a path filter the schema no longer advertises is the one way a bound
/// session could still read across workspaces.
/// </para>
/// </remarks>
public sealed class McpWorkspaceArgumentBinder(IBoundWorkspaceCatalog catalog)
{
    public async Task<McpWorkspaceBindingDecision> BindAsync(
        string boundWorkspace,
        IReadOnlyDictionary<string, JsonElement> arguments,
        IReadOnlyList<string> targetParameters,
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

            if (IsWorkspaceIdParameter(key, targetParameters))
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

        var idParameters = targetParameters.Where(name => IsWorkspaceIdParameter(name, targetParameters)).ToArray();
        if (idParameters.Length == 0)
        {
            if (!TryInjectWorktreePath(boundWorkspace, targetParameters, rewritten))
            {
                return McpWorkspaceBindingDecision.Forbid(
                    $"Workspace '{boundWorkspace}' bound to this token is not registered.");
            }

            return McpWorkspaceBindingDecision.Allow(rewritten);
        }

        foreach (var name in idParameters.Where(name => !HasText(rewritten, name)))
            rewritten[name] = JsonSerializer.SerializeToElement(boundWorkspace);
        foreach (var name in rewritten.Keys.Where(McpWorkspaceTargetArguments.IsPathName).ToArray())
            rewritten.Remove(name);

        return McpWorkspaceBindingDecision.Allow(rewritten);
    }

    /// <summary>
    /// Fills in the path of a tool whose only workspace target is a Server-host path. A
    /// tool that also takes an id is pinned by that id instead, so it is left alone.
    /// Returns false when the tool needs a path and the bound workspace is not registered.
    /// </summary>
    private bool TryInjectWorktreePath(
        string boundWorkspace,
        IReadOnlyList<string> targetParameters,
        Dictionary<string, JsonElement> rewritten)
    {
        var pathParameters = targetParameters
            .Where(McpWorkspaceTargetArguments.IsPathName)
            .Where(name => !HasText(rewritten, name))
            .ToArray();
        if (pathParameters.Length == 0)
            return true;

        var worktreePath = catalog.WorktreePathFor(boundWorkspace);
        if (string.IsNullOrWhiteSpace(worktreePath))
            return false;

        foreach (var name in pathParameters)
            rewritten[name] = JsonSerializer.SerializeToElement(worktreePath);
        return true;
    }

    private static bool IsWorkspaceIdParameter(string key, IReadOnlyList<string> targetParameters)
        => !McpWorkspaceTargetArguments.IsPathName(key)
           && (McpWorkspaceTargetArguments.IsAlwaysBoundName(key)
               || targetParameters.Contains(key, StringComparer.OrdinalIgnoreCase));

    private static bool HasText(IReadOnlyDictionary<string, JsonElement> arguments, string name)
        => arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(ReadArgumentText(value));

    private static string? ReadArgumentText(JsonElement value)
        => value.ValueKind is JsonValueKind.String ? value.GetString() : value.GetRawText();

    private static string Refusal(string boundWorkspace, string target)
        => $"This token is bound to workspace '{boundWorkspace}' and cannot target '{target}'.";
}
