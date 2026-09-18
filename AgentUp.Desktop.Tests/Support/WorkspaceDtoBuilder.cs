using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Workspaces.DTOs;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Builds a <see cref="WorkspaceDto"/> from the canonical workspace in
/// <see cref="DesktopDomain"/>, so a test states only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This replaces the canned WorkspaceFixtures methods it grew out of: "the same workspace
/// but stopped" is <c>DesktopDomain.Workspace().InState(DesktopDomain.StoppedState)</c>
/// rather than another method on a shared file.
/// </remarks>
internal sealed class WorkspaceDtoBuilder
{
    private string _id = DesktopDomain.WorkspaceId;
    private string _displayName = DesktopDomain.WorkspaceName;
    private string _repositoryPath = DesktopDomain.RepositoryPath;
    private string _worktreePath = DesktopDomain.WorktreePath;
    private string _branch = DesktopDomain.Branch;
    private string _commit = DesktopDomain.Commit;
    private string _state = DesktopDomain.RunningState;
    private readonly List<ApplicationDto> _applications = [];
    private DateTimeOffset _lastActivityAtUtc;

    /// <summary>
    /// Names the workspace by id and derives its display name and paths from that id, the
    /// shape tests use when only "which workspace" matters.
    /// </summary>
    public WorkspaceDtoBuilder Identified(string id)
    {
        _id = id;
        _displayName = id;
        _repositoryPath = $"/repo/{id}";
        _worktreePath = $"/worktrees/{id}";
        return this;
    }

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

    public WorkspaceDtoBuilder Stopped() => InState(DesktopDomain.StoppedState);

    public WorkspaceDtoBuilder WithApplication(ApplicationDtoBuilder application)
    {
        _applications.Add(application.Build());
        return this;
    }

    public WorkspaceDtoBuilder WithApplication(ApplicationDto application)
    {
        _applications.Add(application);
        return this;
    }

    public WorkspaceDtoBuilder ActiveAt(DateTimeOffset lastActivityAtUtc)
    {
        _lastActivityAtUtc = lastActivityAtUtc;
        return this;
    }

    public WorkspaceDto Build()
        => new(_id, _displayName, _repositoryPath, _worktreePath, _branch, _commit, _state)
        {
            Applications = _applications,
            LastActivityAtUtc = _lastActivityAtUtc
        };
}
