using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Validation.Services;

namespace AgentUp.AUDebug.Features.Validation.Controllers;

public sealed class ValidationController
{
    private readonly ValidationCommandService _service;

    public ValidationController(ValidationCommandService service) => _service = service;

    public Task<CommandResultDto> RunAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => _service.RunAsync(command, cancellationToken);
}
