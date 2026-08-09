using AgentUp.PackageSmoke.Features.SmokeRuns.Services;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Controllers;

public sealed class SmokeCommandController
{
    private readonly SmokeCommandService _service;

    public SmokeCommandController(SmokeCommandService service)
    {
        _service = service;
    }

    public async Task<int> ExecuteAsync(
        string[] args,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
        => await _service.ExecuteAsync(args, standardOutput, standardError, cancellationToken);
}
