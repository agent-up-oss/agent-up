using AgentUp.Installers.Features.PrerequisiteChecks.Models;

namespace AgentUp.Installers.Features.PrerequisiteChecks.Controllers;

public sealed class DockerPrerequisiteController
{
    private readonly DockerPrerequisite _service;

    public DockerPrerequisiteController(DockerPrerequisite service)
    {
        _service = service;
    }

    public async Task<DockerStatus> CheckAsync(CancellationToken cancellationToken = default)
        => await _service.CheckAsync(cancellationToken);
}
