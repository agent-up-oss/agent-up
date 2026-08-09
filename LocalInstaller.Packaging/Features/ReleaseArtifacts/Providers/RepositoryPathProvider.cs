using LocalInstaller.Packaging.Features.ReleaseArtifacts.Interfaces;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.Models;

namespace LocalInstaller.Packaging.Features.ReleaseArtifacts.Providers;

public sealed class RepositoryPathProvider : IRepositoryPathProvider
{
    public string FindRepositoryRoot() => RepositoryPaths.FindRepositoryRoot();
}
