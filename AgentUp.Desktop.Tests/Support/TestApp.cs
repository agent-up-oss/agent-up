using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.ReactiveUI;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(AgentUp.Desktop.Tests.Support.TestApp))]

namespace AgentUp.Desktop.Tests.Support;

public class TestApp : Application
{
    public override void Initialize()
    {
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://AgentUp.Desktop/"))
        {
            Source = new Uri("avares://AgentUp.Desktop/DesignSystem/AgentUpTheme.axaml")
        });
        Styles.Add(new FluentTheme());
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UseReactiveUI();
}
