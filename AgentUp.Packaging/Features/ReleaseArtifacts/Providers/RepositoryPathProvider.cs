using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Models;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

public sealed class RepositoryPathProvider : IRepositoryPathProvider
{
    public string FindRepositoryRoot() => RepositoryPaths.FindRepositoryRoot();
}
