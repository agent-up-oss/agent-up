using LocalInstaller.Core.Features.PrerequisiteChecks.Models;

namespace LocalInstaller.Core.Features.PrerequisiteChecks.Controllers;

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
