using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Mobile.Services;

namespace AgentUp.AUDebug.Features.Mobile.Controllers;

public sealed class MobileController
{
    private readonly MobileCommandService _service;

    public MobileController(MobileCommandService service) => _service = service;

    public Task<CommandResultDto> RunAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => Dispatch(_service, command, cancellationToken);

    private static Task<CommandResultDto> Dispatch(
        MobileCommandService service,
        DebugCommandDto command,
        CancellationToken cancellationToken)
        => command.Action switch
        {
            "screenshot" => service.ScreenshotAsync(command, cancellationToken),
            "login" => service.LoginAsync(command, cancellationToken),
            _ => Task.FromResult(CommandResultDto.Fail($"Error: unknown mobile action '{command.Action}'."))
        };
}
