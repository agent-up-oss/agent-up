using AgentUp.AUDebug.Features.Desktop.Services;
using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Features.Desktop.Controllers;

public sealed class DesktopController
{
    private readonly DesktopCommandService _service;

    public DesktopController(DesktopCommandService service) => _service = service;

    public Task<CommandResultDto> RunAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => Dispatch(_service, command, cancellationToken);

    private static Task<CommandResultDto> Dispatch(
        DesktopCommandService service,
        DebugCommandDto command,
        CancellationToken cancellationToken)
        => command.Action switch
        {
            "screenshot" => service.ScreenshotAsync(command, cancellationToken),
            "login" => service.LoginAsync(command, cancellationToken),
            "start-workspace" => service.StartWorkspaceAsync(command, cancellationToken),
            _ => Task.FromResult(CommandResultDto.Fail($"Error: unknown desktop action '{command.Action}'."))
        };
}
