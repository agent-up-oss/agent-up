using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// Builds an <see cref="EnqueueTaskRequest"/> so a test states only the workspace, issue, or
/// repository it is actually about.
/// </summary>
internal sealed class EnqueueTaskRequestBuilder
{
    private string? _workspaceId;
    private string _issueId = "I-57";
    private string? _targetWorkspaceId;
    private string? _moduleId;
    private string? _owner;
    private string? _repository;

    public EnqueueTaskRequestBuilder FromWorkspace(string? workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    public EnqueueTaskRequestBuilder Issue(string issueId)
    {
        _issueId = issueId;
        return this;
    }

    public EnqueueTaskRequestBuilder Targeting(string? workspaceId)
    {
        _targetWorkspaceId = workspaceId;
        return this;
    }

    public EnqueueTaskRequestBuilder FromRepository(string moduleId, string owner, string repository)
    {
        _moduleId = moduleId;
        _owner = owner;
        _repository = repository;
        return this;
    }

    public EnqueueTaskRequest Build() =>
        new(_workspaceId, _issueId, _targetWorkspaceId, _moduleId, _owner, _repository);
}
