using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// Builds a <see cref="RegisterWorkspaceRequest"/> from the canonical workspace in
/// <see cref="ServerDomain"/>, so a test states only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="RegisterWorkspaceRequest"/> positionally.
/// The record carries five positional fields and five collection properties; adding one
/// changes this file instead of the hundred-odd registrations across the suite.
/// </remarks>
internal sealed class RegisterWorkspaceRequestBuilder
{
    private string _displayName = ServerDomain.WorkspaceName;
    private string _repositoryPath = ServerDomain.RepositoryPath;
    private string _worktreePath = ServerDomain.WorktreePath;
    private string _branch = ServerDomain.Branch;
    private string _commit = ServerDomain.Commit;
    private readonly List<ApplicationDefinition> _applications = [];
    private readonly List<DesktopApplicationDefinition> _desktopApplications = [];
    private readonly List<DockerServiceDefinition> _services = [];
    private readonly List<DotnetApplicationDefinition> _dotnet = [];
    private readonly List<DockerCapabilityDefinition> _docker = [];

    public RegisterWorkspaceRequestBuilder Named(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    /// <summary>Puts the repository and its worktree at the same path, the common case.</summary>
    public RegisterWorkspaceRequestBuilder At(string path)
    {
        _repositoryPath = path;
        _worktreePath = path;
        return this;
    }

    public RegisterWorkspaceRequestBuilder WithRepositoryPath(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
        return this;
    }

    public RegisterWorkspaceRequestBuilder WithWorktreePath(string worktreePath)
    {
        _worktreePath = worktreePath;
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

    public RegisterWorkspaceRequestBuilder WithApplication(ApplicationDefinitionBuilder application)
    {
        _applications.Add(application.Build());
        return this;
    }

    public RegisterWorkspaceRequestBuilder WithApplication(ApplicationDefinition application)
    {
        _applications.Add(application);
        return this;
    }

    public RegisterWorkspaceRequestBuilder WithApplications(IEnumerable<ApplicationDefinition> applications)
    {
        _applications.AddRange(applications);
        return this;
    }

    public RegisterWorkspaceRequestBuilder WithDesktopApplication(DesktopApplicationDefinition application)
    {
        _desktopApplications.Add(application);
        return this;
    }

    public RegisterWorkspaceRequestBuilder WithService(DockerServiceDefinition service)
    {
        _services.Add(service);
        return this;
    }

    public RegisterWorkspaceRequestBuilder WithDotnet(DotnetApplicationDefinition application)
    {
        _dotnet.Add(application);
        return this;
    }

    public RegisterWorkspaceRequestBuilder WithDocker(DockerCapabilityDefinition capability)
    {
        _docker.Add(capability);
        return this;
    }

    public RegisterWorkspaceRequest Build()
        => new(_displayName, _repositoryPath, _worktreePath, _branch, _commit)
        {
            Applications = _applications,
            DesktopApplications = _desktopApplications,
            Services = _services,
            Dotnet = _dotnet,
            Docker = _docker
        };
}
