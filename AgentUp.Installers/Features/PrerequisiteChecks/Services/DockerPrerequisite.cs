using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;

namespace AgentUp.Installers.Features.PrerequisiteChecks.Services;

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
