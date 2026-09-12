using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Test.Services;

namespace AgentUp.AUDebug.Features.Test.Controllers;

public sealed class TestController
{
    private readonly TestCommandService _service;

    public TestController(TestCommandService service) => _service = service;

    public Task<CommandResultDto> RunAsync(DebugCommandDto command, CancellationToken cancellationToken)
        => _service.RunAsync(command, cancellationToken);
}
