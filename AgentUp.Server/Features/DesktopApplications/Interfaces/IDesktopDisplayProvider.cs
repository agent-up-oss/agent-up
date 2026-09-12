using AgentUp.Server.Features.DesktopApplications.Models;

namespace AgentUp.Server.Features.DesktopApplications.Interfaces;

public interface IDesktopDisplayProvider
{
    Task<DesktopDisplayHandle> StartAsync(int width, int height, CancellationToken cancellationToken);
    Task<byte[]> CapturePngAsync(DesktopDisplayHandle display, CancellationToken cancellationToken);
    Task SendPointerAsync(DesktopDisplayHandle display, int x, int y, int button, bool pressed, CancellationToken cancellationToken);
    Task SendKeyAsync(DesktopDisplayHandle display, string key, bool pressed, CancellationToken cancellationToken);
    Task StopAsync(DesktopDisplayHandle display, CancellationToken cancellationToken);
}
