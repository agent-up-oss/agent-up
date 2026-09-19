using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Tests.Support;

/// <summary>
/// Builds a <see cref="RegisterWorkspaceRequest"/> for the end-to-end suite, so a test
/// states only the workspace attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="RegisterWorkspaceRequest"/> positionally;
/// adding a field to the record changes this file instead of every registration.
/// </remarks>
internal sealed class RegisterWorkspaceRequestBuilder
{
    private string _displayName = "workspace";
    private string _repositoryPath = "/repo";
    private string _worktreePath = "/repo";
    private string _branch = ProductDomain.Branch;
    private string _commit = ProductDomain.Commit;
    private readonly List<ApplicationDefinition> _applications = [];
    private readonly List<DesktopApplicationDefinition> _desktopApplications = [];

    public RegisterWorkspaceRequestBuilder Named(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    /// <summary>Puts the repository and its worktree at the same path, as the suite does.</summary>
    public RegisterWorkspaceRequestBuilder At(string path)
    {
        _repositoryPath = path;
        _worktreePath = path;
        return this;
    }

    public RegisterWorkspaceRequestBuilder OnBranch(string branch)
    {
        _branch = branch;
        return this;
    }

    public RegisterWorkspaceRequestBuilder AtCommit(string commit)
    {
        _commit = commit;
        return this;
    }

    public RegisterWorkspaceRequestBuilder WithApplication(ApplicationDefinition application)
    {
        _applications.Add(application);
        return this;
    }

    public RegisterWorkspaceRequestBuilder WithDesktopApplication(DesktopApplicationDefinition application)
    {
        _desktopApplications.Add(application);
        return this;
    }

    public RegisterWorkspaceRequest Build()
        => new(_displayName, _repositoryPath, _worktreePath, _branch, _commit)
        {
            Applications = _applications,
            DesktopApplications = _desktopApplications
        };
}
