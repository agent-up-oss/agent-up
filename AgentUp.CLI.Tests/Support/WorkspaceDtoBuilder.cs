using AgentUp.CLI.Features.Workspaces.DTOs;

namespace AgentUp.CLI.Tests.Support;

/// <summary>
/// Builds a <see cref="WorkspaceDto"/> from the canonical workspace in
/// <see cref="CliDomain"/>, so a test states only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="WorkspaceDto"/> positionally. Six of its
/// seven fields are strings, which positionally read as nothing at all.
/// </remarks>
internal sealed class WorkspaceDtoBuilder
{
    private string _id = CliDomain.WorkspaceId;
    private string _displayName = CliDomain.WorkspaceName;
    private string _repositoryPath = CliDomain.RepositoryPath;
    private string _worktreePath = CliDomain.WorktreePath;
    private string _branch = CliDomain.Branch;
    private string _commit = CliDomain.Commit;
    private string _state = "Running";
    private readonly List<ApplicationDefinition> _applications = [];
    private string? _lastError;

    public WorkspaceDtoBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public WorkspaceDtoBuilder Named(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    public WorkspaceDtoBuilder At(string path)
    {
        _repositoryPath = path;
        _worktreePath = path;
        return this;
    }

    public WorkspaceDtoBuilder WithRepositoryPath(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
        return this;
    }

    public WorkspaceDtoBuilder WithWorktreePath(string worktreePath)
    {
        _worktreePath = worktreePath;
        return this;
    }

    public WorkspaceDtoBuilder OnBranch(string branch)
    {
        _branch = branch;
        return this;
    }

    public WorkspaceDtoBuilder AtCommit(string commit)
    {
        _commit = commit;
        return this;
    }

    public WorkspaceDtoBuilder InState(string state)
    {
        _state = state;
        return this;
    }

    public WorkspaceDtoBuilder WithApplication(ApplicationDefinition application)
    {
        _applications.Add(application);
        return this;
    }

    public WorkspaceDtoBuilder WithLastError(string? lastError)
    {
        _lastError = lastError;
        return this;
    }

    public WorkspaceDto Build()
        => new(_id, _displayName, _repositoryPath, _worktreePath, _branch, _commit, _state)
        {
            Applications = _applications,
            LastError = _lastError
        };
}
