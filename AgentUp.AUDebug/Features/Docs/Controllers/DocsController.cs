using AgentUp.AUDebug.Features.Docs.Services;
using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Features.Docs.Controllers;

public sealed class DocsController
{
    private readonly DocsCommandService _service;

    public DocsController(DocsCommandService service) => _service = service;

    public Task<CommandResultDto> ScreenshotAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => _service.ScreenshotAsync(command, cancellationToken);
}
