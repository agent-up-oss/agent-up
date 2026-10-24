using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Validate.Services;

namespace AgentUp.AUDebug.Features.Validate.Controllers;

public sealed class ValidateController
{
    private readonly ValidateCommandService _service;

    public ValidateController(ValidateCommandService service) => _service = service;

    public Task<CommandResultDto> RunAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => _service.RunAsync(command, cancellationToken);
}
