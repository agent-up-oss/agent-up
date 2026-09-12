using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAgentProcessProvider : IAsyncDisposable
{
    event Func<string, JsonElement, Task>? Notification;
    event Func<string, JsonElement, Task<JsonElement>>? Request;
    event Action<string?>? Exited;
    Task StartAsync(AgentKind kind, string workingDirectory, CancellationToken cancellationToken);
    Task<JsonElement> CallAsync(string method, object? parameters, CancellationToken cancellationToken);
    Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
