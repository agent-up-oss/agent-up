using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

[TestFixture]
public class WindowChromeBehaviorTests
{
    [AvaloniaTest]
    public async Task Window_usesIntegratedChrome_insteadOfSystemDecorations()
    {
        var app = await AppDriver.LaunchEmptyAsync();

        Assert.That(app.Window.WindowDecorations, Is.EqualTo(WindowDecorations.None));
        Assert.That(app.Window.FindControl<Border>("WindowChrome"), Is.Not.Null);
        Assert.That(app.Window.FindControl<Button>("SidebarToggle"), Is.Not.Null);
        Assert.That(app.Window.FindControl<Button>("ReloadButton"), Is.Not.Null);
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

        Assert.That(app.Window.FindControl<TextBlock>("ServerStatusText")?.Text, Is.EqualTo("SERVER ONLINE"));
        Assert.That(app.Window.FindControl<Border>("ServerStatusBadge")?.BorderBrush?.ToString(), Is.EqualTo("#ff00d66b"));
    }

    [AvaloniaTest]
    public async Task ServerBadge_isRed_whenServerCannotBeReached()
    {
        var app = await AppDriver.LaunchWithServerErrorAsync();

        Assert.That(app.Window.FindControl<TextBlock>("ServerStatusText")?.Text, Is.EqualTo("SERVER OFFLINE"));
        Assert.That(app.Window.FindControl<Border>("ServerStatusBadge")?.BorderBrush?.ToString(), Is.EqualTo("#ffd84f4f"));
    }
}
