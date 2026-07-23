using AgentUp.Desktop.Features.Console.Providers;

namespace AgentUp.Desktop.Features.Console.Services;

public sealed class ConsoleOutputService
{
    private readonly ConsoleApiClient _client;

    public ConsoleOutputService(ConsoleApiClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<string>> GetOutputAsync(
        string workspaceId,
        string applicationName,
        CancellationToken cancellationToken = default)
        => await _client.GetOutputAsync(workspaceId, applicationName, cancellationToken);
}
