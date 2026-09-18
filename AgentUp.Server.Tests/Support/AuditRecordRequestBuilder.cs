using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// Builds an <see cref="AuditRecordRequest"/> from a sensible default, so a test states
/// only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="AuditRecordRequest"/> positionally. Four of
/// its eight fields are free-form strings of the same type, which positionally read as
/// nothing at all.
/// </remarks>
internal sealed class AuditRecordRequestBuilder
{
    private string _kind = "workspace";
    private string _source = "server";
    private string _action = "register";
    private string _outcome = "succeeded";
    private string? _workspaceId;
    private Dictionary<string, string>? _details;
    private List<string>? _artifactIds;
    private string? _scope;

    public AuditRecordRequestBuilder OfKind(string kind)
    {
        _kind = kind;
        return this;
    }

    public AuditRecordRequestBuilder From(string source)
    {
        _source = source;
        return this;
    }

    public AuditRecordRequestBuilder Doing(string action)
    {
        _action = action;
        return this;
    }

    public AuditRecordRequestBuilder Outcome(string outcome)
    {
        _outcome = outcome;
        return this;
    }

    public AuditRecordRequestBuilder ForWorkspace(string? workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    public AuditRecordRequestBuilder WithDetail(string key, string value)
    {
        _details ??= [];
        _details[key] = value;
        return this;
    }

    public AuditRecordRequestBuilder WithDetails(IReadOnlyDictionary<string, string> details)
    {
        _details ??= [];
        foreach (var (key, value) in details)
            _details[key] = value;
        return this;
    }

    public AuditRecordRequestBuilder WithArtifacts(params string[] artifactIds)
    {
        _artifactIds ??= [];
        _artifactIds.AddRange(artifactIds);
        return this;
    }

    public AuditRecordRequestBuilder InScope(string? scope)
    {
        _scope = scope;
        return this;
    }

    public AuditRecordRequest Build()
        => new(_kind, _source, _action, _outcome, _workspaceId, _details, _artifactIds, _scope);
}
