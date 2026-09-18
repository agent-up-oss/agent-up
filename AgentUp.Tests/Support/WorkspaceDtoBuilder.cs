using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Workspaces.DTOs;

namespace AgentUp.Tests.Support;

/// <summary>
/// Builds a <see cref="WorkspaceDto"/> - the Desktop's reading of a workspace - for the
/// end-to-end suite, so a test states only the attribute it is actually about.
/// </summary>
internal sealed class WorkspaceDtoBuilder
{
    private string _id = "workspace";
    private string _displayName = "workspace";
    private string _repositoryPath = "/repo";
    private string _worktreePath = "/repo";
    private string _branch = ProductDomain.Branch;
    private string _commit = ProductDomain.Commit;
    private string _state = ProductDomain.RunningState;
    private readonly List<ApplicationDto> _applications = [];

    /// <summary>
    /// Names the workspace by id and derives its display name and paths from that id, the
    /// shape the suite uses when only "which workspace" matters.
    /// </summary>
    public WorkspaceDtoBuilder Identified(string id)
    {
        _id = id;
        _displayName = id;
        _repositoryPath = $"/repo/{id}";
        _worktreePath = $"/worktrees/{id}";
        return this;
    }

    public WorkspaceDtoBuilder InState(string state)
    {
        _state = state;
        return this;
    }

    public WorkspaceDtoBuilder WithApplication(ApplicationDtoBuilder application)
    {
        _applications.Add(application.Build());
        return this;
    }

    public WorkspaceDto Build()
        => new(_id, _displayName, _repositoryPath, _worktreePath, _branch, _commit, _state)
        {
            Applications = _applications
        };
}
