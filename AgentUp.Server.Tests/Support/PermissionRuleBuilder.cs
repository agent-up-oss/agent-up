using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Support;

/// <summary>One workspace permission rule: allowed and unscoped unless a test says otherwise.</summary>
internal sealed class PermissionRuleBuilder(string id, PermissionRuleKind kind)
{
    private bool _allow = true;
    private string? _scope;

    public PermissionRuleBuilder Denied()
    {
        _allow = false;
        return this;
    }

    public PermissionRuleBuilder Scoped(string scope)
    {
        _scope = scope;
        return this;
    }

    public PermissionRule Build() => new(id, kind, _allow, _scope);
}
