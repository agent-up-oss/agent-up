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
