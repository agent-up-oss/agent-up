using AgentUp.Desktop.Features.Console.Services;

namespace AgentUp.Desktop.Features.Console.Controllers;

public sealed class ConsoleController
{
    private readonly ConsoleOutputService _service;

    public ConsoleController(ConsoleOutputService service)
    {
        _service = service;
    }

    public async Task<IReadOnlyList<string>> GetOutputAsync(
        string workspaceId,
        string applicationName,
        CancellationToken cancellationToken = default)
        => await _service.GetOutputAsync(workspaceId, applicationName, cancellationToken);
}
