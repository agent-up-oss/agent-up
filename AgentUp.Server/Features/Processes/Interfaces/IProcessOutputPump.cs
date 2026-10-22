using AgentUp.Server.Features.Processes.Models;

namespace AgentUp.Server.Features.Processes.Interfaces;

public interface IProcessOutputPump
{
    Task DrainAsync(
        StreamReader reader,
        ProcessOutputStream stream,
        Func<string, ProcessOutputStream, CancellationToken, Task> onLine,
        CancellationToken cancellationToken = default);
}
