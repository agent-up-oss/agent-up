using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Features.Host.Interfaces;

public interface IHostProcessSupervisor
{
    Task<HostSessionDto> StartAsync(CancellationToken cancellationToken);
    Task StopAsync(HostSessionDto session, CancellationToken cancellationToken);
    Task WaitAsync(CancellationToken cancellationToken);
    bool HasLiveProcess(HostSessionDto session);
}
