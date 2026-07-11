using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(AgentUp.Desktop.Tests.Support.TestApp))]

namespace AgentUp.Desktop.Tests.Support;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UseReactiveUI();
}
