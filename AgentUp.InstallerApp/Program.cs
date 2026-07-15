using Avalonia;
using Avalonia.ReactiveUI;
using AgentUp.InstallerApp;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .UseReactiveUI()
    .StartWithClassicDesktopLifetime(args);
