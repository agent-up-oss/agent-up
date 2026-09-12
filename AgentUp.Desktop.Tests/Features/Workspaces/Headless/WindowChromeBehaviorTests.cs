using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

[TestFixture]
public class WindowChromeBehaviorTests
{
    [AvaloniaTest]
    public void Application_resolvesCanonicalDesignSystemBrushes()
    {
        var application = Application.Current ?? throw new InvalidOperationException("Avalonia application is unavailable.");
        Assert.That(application.TryFindResource("AgentUpColorCanvasBrush", out var canvas), Is.True);
        Assert.That(canvas, Is.TypeOf<SolidColorBrush>());
        Assert.That(((SolidColorBrush)canvas!).Color, Is.EqualTo(Color.Parse("#000000")));
        Assert.That(application.TryFindResource("AgentUpColorAccentBrush", out var accent), Is.True);
        Assert.That(accent, Is.TypeOf<SolidColorBrush>());
        Assert.That(((SolidColorBrush)accent!).Color, Is.EqualTo(Color.Parse("#00b850")));
        Assert.That(application.TryFindResource("AgentUpControlHeight", out var height), Is.True);
        Assert.That(height, Is.EqualTo(44d));
        Assert.That(application.TryFindResource("AgentUpCornerRadiusMd", out var radius), Is.True);
        Assert.That(radius, Is.TypeOf<CornerRadius>());
    }

    [AvaloniaTest]
    public void Application_appliesCanonicalButtonStyleInferredFromCss()
    {
        var window = new Window { Width = 200, Height = 80 };
        var button = new Button { Classes = { "au-button" }, Content = "Start workspace" };
        window.Content = button;
        window.Show();

        Assert.That(button.MinHeight, Is.EqualTo(44d));
        Assert.That(button.Background, Is.TypeOf<SolidColorBrush>());
        Assert.That(((SolidColorBrush)button.Background!).Color, Is.EqualTo(Color.Parse("#00b850")));
    }

    [AvaloniaTest]
    public void Application_appliesCanonicalWorkspaceEntryStyleInferredFromCss()
    {
        var window = new Window { Width = 280, Height = 80 };
        var entry = new Border { Classes = { "wsEntry" }, Width = 240, Height = 48 };
        window.Content = entry;
        window.Show();

        Assert.That(entry.Background, Is.TypeOf<SolidColorBrush>());
        Assert.That(((SolidColorBrush)entry.Background!).Color, Is.EqualTo(Color.Parse("#1c1c1c")));
        Assert.That(entry.CornerRadius, Is.EqualTo(new CornerRadius(8)));
    }

    [AvaloniaTest]
    public void Application_appliesCanonicalWorkspaceNameStyleInferredFromCss()
    {
        var window = new Window { Width = 280, Height = 80 };
        var name = new TextBlock { Classes = { "wsName" }, Text = "checkout-fix" };
        window.Content = name;
        window.Show();

        Assert.That(name.FontSize, Is.EqualTo(14d));
        Assert.That(name.Foreground, Is.TypeOf<SolidColorBrush>());
        Assert.That(((SolidColorBrush)name.Foreground!).Color, Is.EqualTo(Color.Parse("#f5fbf7")));
    }

    [AvaloniaTest]
    public async Task Window_usesIntegratedChrome_insteadOfSystemDecorations()
    {
        var app = await AppDriver.LaunchEmptyAsync();

        Assert.That(app.Window.WindowDecorations, Is.EqualTo(WindowDecorations.None));
        Assert.That(app.Window.FindControl<Border>("WindowChrome"), Is.Not.Null);
        Assert.That(ChromeTestSupport.FindDescendantByName<Button>(app.Window, "SidebarToggle"), Is.Not.Null);
        Assert.That(ChromeTestSupport.FindDescendantByName<Button>(app.Window, "ReloadButton"), Is.Not.Null);
        Assert.That(app.Window.FindControl<Button>("MinimizeWindowButton"), Is.Not.Null);
        Assert.That(app.Window.FindControl<Button>("RestoreWindowButton"), Is.Not.Null);
        Assert.That(app.Window.FindControl<Button>("CloseWindowButton"), Is.Not.Null);
    }

    [AvaloniaTest]
    public async Task RestoreButton_togglesWindowState()
    {
        var app = await AppDriver.LaunchEmptyAsync();
        var restore = app.Window.FindControl<Button>("RestoreWindowButton")
            ?? throw new InvalidOperationException("Restore button was not found.");

        await app.Window.ClickControlAsync(restore);

        Assert.That(app.Window.WindowState, Is.EqualTo(WindowState.Maximized));

        await app.Window.ClickControlAsync(restore);

        Assert.That(app.Window.WindowState, Is.EqualTo(WindowState.Normal));
    }

    [AvaloniaTest]
    public async Task ServerBadge_isGreen_whenServerCanBeReached()
    {
        var app = await AppDriver.LaunchEmptyAsync();

        Assert.That(ChromeTestSupport.FindDescendantByName<TextBlock>(app.Window, "ServerStatusText")?.Text, Is.EqualTo("SERVER ONLINE"));
        Assert.That(ChromeTestSupport.FindDescendantByName<Border>(app.Window, "ServerStatusBadge")?.BorderBrush?.ToString(), Is.EqualTo("#ff00d66b"));
    }

    [AvaloniaTest]
    public async Task ServerBadge_isRed_whenServerCannotBeReached()
    {
        var app = await AppDriver.LaunchWithServerErrorAsync();

        Assert.That(ChromeTestSupport.FindDescendantByName<TextBlock>(app.Window, "ServerStatusText")?.Text, Is.EqualTo("SERVER OFFLINE"));
        Assert.That(ChromeTestSupport.FindDescendantByName<Border>(app.Window, "ServerStatusBadge")?.BorderBrush?.ToString(), Is.EqualTo("#ffd84f4f"));
    }
}
