using Avalonia;
using Avalonia.ReactiveUI;

namespace AgentUp.Tests.Fixtures;

public interface IDesktopFixtureAdapter : IDisposable
{
    string Name { get; }
    bool RequiresStaThread { get; }
    bool RequiresSetupThreadAvalonia { get; }
    string StartupFailureHint { get; }
    void SetUp();
}

public static class DesktopFixtureAdapter
{
    public static IDesktopFixtureAdapter Create()
    {
        if (OperatingSystem.IsLinux())
            return new Linux.LinuxDesktopFixtureAdapter();
        if (OperatingSystem.IsMacOS())
            return new MacOs.MacOsDesktopFixtureAdapter();
        if (OperatingSystem.IsWindows())
            return new Windows.WindowsDesktopFixtureAdapter();

        throw new PlatformNotSupportedException("AgentUp.Tests requires Linux, macOS, or Windows.");
    }

    public static AppBuilder ConfigureAvalonia<TApp>() where TApp : Application, new() =>
        AppBuilder.Configure<TApp>()
            .UsePlatformDetect()
            .UseReactiveUI()
            .AfterSetup(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(AgentUp.Tests.DesktopFixtureHost.MarkAvaloniaReady);
            });
}
