using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeSupervisor : IHostProcessSupervisor
{
    public HostSessionDto Session { get; set; } = new(
        1,
        "/repo",
        "/repo/.git/agent-up/au-debug",
        [
            new HostedProcessDto("server", 11, "/repo/.git/agent-up/au-debug/logs/server.log", "http://127.0.0.1:5001"),
            new HostedProcessDto("desktop", 12, "/repo/.git/agent-up/au-debug/logs/desktop.log", null),
            new HostedProcessDto("mobile", 13, "/repo/.git/agent-up/au-debug/logs/mobile.log", "http://127.0.0.1:10102"),
            new HostedProcessDto("docs", 14, "/repo/.git/agent-up/au-debug/logs/docs.log", "http://127.0.0.1:10100")
        ]);

    public bool Live { get; set; }
    public bool ThrowTimeout { get; set; }
    public bool DelayUntilCanceled { get; set; }
    public int Starts { get; private set; }
    public int Stops { get; private set; }
    public int Waits { get; private set; }

    public Task<HostSessionDto> StartAsync(CancellationToken cancellationToken)
    {
        Starts++;
        cancellationToken.ThrowIfCancellationRequested();
        Live = true;
        return Task.FromResult(Session);
    }

    public async Task StopAsync(HostSessionDto session, CancellationToken cancellationToken)
    {
        Stops++;
        if (DelayUntilCanceled)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        Live = false;
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        Waits++;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public bool HasLiveProcess(HostSessionDto session) => Live;
}
