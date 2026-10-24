using AgentUp.Server.Features.Applications.DTOs;

namespace AgentUp.Server.Features.Workspaces.Models;

internal enum ApplicationReadinessKind
{
    HealthCheck,
    TcpPort,
    Container,
    Process
}

internal sealed record ApplicationReadinessSignal(
    ApplicationReadinessKind Kind,
    int? Port = null,
    string? Path = null)
{
    public static ApplicationReadinessSignal For(ApplicationInstance app)
    {
        var health = app.AllocatedPorts.FirstOrDefault(port => !string.IsNullOrWhiteSpace(port.HealthCheckPath));
        if (health is not null)
            return new(ApplicationReadinessKind.HealthCheck, health.AllocatedPort, health.HealthCheckPath);

        var port = app.AllocatedPorts.FirstOrDefault();
        if (port is not null)
            return new(ApplicationReadinessKind.TcpPort, port.AllocatedPort);

        if (app.ServiceType == ServiceType.Docker)
            return new(ApplicationReadinessKind.Container);

        return new(ApplicationReadinessKind.Process);
    }

    public string Description => Kind switch
    {
        ApplicationReadinessKind.HealthCheck => $"http://127.0.0.1:{Port}{Path}",
        ApplicationReadinessKind.TcpPort => $"port {Port}",
        ApplicationReadinessKind.Container => "container",
        _ => "process"
    };

    public string TimeoutMessage(TimeSpan deadline)
    {
        var seconds = (int)Math.Ceiling(deadline.TotalSeconds);
        return Kind switch
        {
            ApplicationReadinessKind.HealthCheck => $"{Description} never succeeded within {seconds}s",
            ApplicationReadinessKind.TcpPort => $"port {Port} never accepted a connection within {seconds}s",
            ApplicationReadinessKind.Container => $"container never became running within {seconds}s",
            _ => $"process never stayed running within {seconds}s"
        };
    }
}
