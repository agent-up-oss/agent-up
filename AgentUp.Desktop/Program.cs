using Avalonia;
using Avalonia.ReactiveUI;
using AgentUp.Desktop.Composition;
using AgentUp.InstallerConfig;

namespace AgentUp.Desktop;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        RepositoryDotEnv.LoadOptional();
        using var sentry = SentryTelemetry.Initialize();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}