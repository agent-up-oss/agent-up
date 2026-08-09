using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Services;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;

public sealed class PayloadStagingController
{
    private readonly PackagePayloadStager _stager;

    public PayloadStagingController(PackagePayloadStager stager)
    {
        _stager = stager;
    }

    public Task StageAsync(PayloadStagingRequest request, CancellationToken cancellationToken = default)
        => _stager.StageAsync(request, cancellationToken);
}
