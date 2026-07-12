using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using Avalonia.Themes.Fluent;
using Avalonia.WebView.Desktop;

[assembly: AvaloniaTestApplication(typeof(AgentUp.Tests.Support.TestApp))]

namespace AgentUp.Tests.Support;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UsePlatformDetect()
            .UseReactiveUI()
            .UseDesktopWebView();
}
