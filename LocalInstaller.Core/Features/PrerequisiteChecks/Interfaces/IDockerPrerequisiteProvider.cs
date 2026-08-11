using LocalInstaller.Core.Features.PrerequisiteChecks.Models;

namespace LocalInstaller.Core.Features.PrerequisiteChecks.Interfaces;

public interface IDockerPrerequisiteProvider
{
    Task<DockerStatus> CheckAsync(Version minimumVersion, CancellationToken cancellationToken = default);
}
