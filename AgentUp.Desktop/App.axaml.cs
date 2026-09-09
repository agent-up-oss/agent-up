using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AgentUp.Desktop.Composition;

namespace AgentUp.Desktop;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = AppComposition.InitializeDesktopAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
