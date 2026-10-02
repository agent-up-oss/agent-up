using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.Services;

namespace AgentUp.AUDebug.Features.Screens.Controllers;

public sealed class ScreensController
{
    private readonly ScreensCommandService _service;

    public ScreensController(ScreensCommandService service) => _service = service;

    public Task<CommandResultDto> RunAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => _service.CaptureAsync(command, cancellationToken);
}
