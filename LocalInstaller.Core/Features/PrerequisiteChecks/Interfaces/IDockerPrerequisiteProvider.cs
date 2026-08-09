using AgentUp.Installers.Features.PrerequisiteChecks.Models;

namespace AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;

public interface IDockerPrerequisiteProvider
{
    Task<DockerStatus> CheckAsync(Version minimumVersion, CancellationToken cancellationToken = default);
}
