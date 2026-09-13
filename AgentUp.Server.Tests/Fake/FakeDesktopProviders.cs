using System.Diagnostics;
using AgentUp.Server.Features.DesktopApplications.Interfaces;
using AgentUp.Server.Features.DesktopApplications.Models;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeHostedDesktopNativeLibraryProvider : IHostedDesktopNativeLibraryProvider
{
    public IReadOnlyDictionary<string, string> CreateEnvironment(string? searchRoot) =>
        new Dictionary<string, string> { ["LD_LIBRARY_PATH"] = "/nix/store/fake-fontconfig/lib" };
}

internal sealed class FakeDesktopDisplayProvider : IDesktopDisplayProvider
{
    public bool Stopped { get; private set; }
    public List<(int X, int Y, int Button, bool Pressed)> PointerEvents { get; } = [];
    public List<(string Key, bool Pressed)> KeyEvents { get; } = [];

    public Task<DesktopDisplayHandle> StartAsync(int width, int height, CancellationToken cancellationToken) =>
        Task.FromResult(new DesktopDisplayHandle(":123", Process.GetCurrentProcess(), width, height, "/tmp/agentup-desktop-fake"));

    public Task<byte[]> CapturePngAsync(DesktopDisplayHandle display, CancellationToken cancellationToken) =>
        Task.FromResult<byte[]>([137, 80, 78, 71]);

    public Task SendPointerAsync(DesktopDisplayHandle display, int x, int y, int button, bool pressed, CancellationToken cancellationToken)
    {
        PointerEvents.Add((x, y, button, pressed));
        return Task.CompletedTask;
    }

    public Task SendKeyAsync(DesktopDisplayHandle display, string key, bool pressed, CancellationToken cancellationToken)
    {
        KeyEvents.Add((key, pressed));
        return Task.CompletedTask;
    }

    public Task StopAsync(DesktopDisplayHandle display, CancellationToken cancellationToken)
    {
        Stopped = true;
        display.DisplayProcess.Dispose();
        return Task.CompletedTask;
    }
}
