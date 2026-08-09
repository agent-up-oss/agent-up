using LocalInstaller.Core.Features.PrerequisiteChecks.Interfaces;
namespace LocalInstaller.Core.Features.PrerequisiteChecks.Models;

public sealed class DockerPrerequisite
{
    private readonly IDockerPrerequisiteProvider _provider;
    private readonly Version _minimumVersion;

    public DockerPrerequisite(IDockerPrerequisiteProvider provider, Version minimumVersion)
    {
        _provider = provider;
        _minimumVersion = minimumVersion;
    }

    public Task<DockerStatus> CheckAsync(CancellationToken cancellationToken = default)
        => _provider.CheckAsync(_minimumVersion, cancellationToken);
}
