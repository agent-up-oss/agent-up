using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screenshots.Services;

namespace AgentUp.AUDebug.Features.Screenshots.Controllers;

public sealed class ScreenshotsController
{
    private readonly ScreenshotCommandService _service;

    public ScreenshotsController(ScreenshotCommandService service) => _service = service;

    public Task<CommandResultDto> RunAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => Route(_service, command, cancellationToken);

    private static Task<CommandResultDto> Route(
        ScreenshotCommandService service,
        DebugCommandDto command,
        CancellationToken cancellationToken)
        => command.Action switch
        {
            "persist" => service.PersistAsync(command, cancellationToken),
            "validate" => service.ValidateAsync(command, cancellationToken),
            "desktop" or "mobile" => service.CaptureAsync(command, cancellationToken),
            _ => Task.FromResult(CommandResultDto.Fail($"Error: unknown screenshots action '{command.Action}'."))
        };
}
