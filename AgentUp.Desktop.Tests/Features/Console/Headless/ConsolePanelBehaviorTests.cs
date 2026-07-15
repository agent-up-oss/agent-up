using Avalonia.Headless.NUnit;
using Avalonia.Controls;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Console.Headless;

[TestFixture]
public class ConsolePanelBehaviorTests
{
    [AvaloniaTest]
    public async Task Panel_loadsConsoleOutput_forAutoSelectedApplication()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var outputLines = new List<string> { "$ cargo run", "▸ Listening on :3001", "› GET / 200" };
        var output = WorkspaceFixtures.OutputFor(workspace.Id, workspace.Applications[0].Name, outputLines);

        var app = await AppDriver.LaunchWithWorkspacesAndOutputAsync([workspace], output);

        Assert.That(app.Content.ConsoleLines, Has.Count.EqualTo(outputLines.Count));
        Assert.That(app.Content.ConsoleLines[0], Is.EqualTo(outputLines[0]));
    }

    [AvaloniaTest]
    public async Task Panel_rendersConsoleOutput_asSelectableReadOnlyText()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var output = WorkspaceFixtures.OutputFor(workspace.Id, workspace.Applications[0].Name, ["copyable error", "second line"]);

        var app = await AppDriver.LaunchWithWorkspacesAndOutputAsync([workspace], output);

        var selectableLine = app.Window.FindControl<SelectableTextBlock>("ConsoleOutput");
        Assert.That(selectableLine, Is.Not.Null);
        Assert.That(selectableLine!.Text, Is.EqualTo($"copyable error{Environment.NewLine}second line"));
        Assert.That(selectableLine.TextWrapping, Is.EqualTo(Avalonia.Media.TextWrapping.Wrap));
    }

    [AvaloniaTest]
    public async Task Panel_updatesConsoleOutput_whenDifferentApplicationSelected()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var output = new Dictionary<string, List<string>>
        {
            [$"{workspace.Id}/{workspace.Applications[0].Name}"] = ["line from API"],
            [$"{workspace.Id}/{workspace.Applications[1].Name}"] = ["line from Docs"],
        };

        var app = await AppDriver.LaunchWithWorkspacesAndOutputAsync([workspace], output);
        await app.Content.SelectApplicationByIndexAsync(1);

        Assert.That(app.Content.SelectedApplicationName, Is.EqualTo(workspace.Applications[1].Name));
        Assert.That(app.Content.ConsoleLines, Has.Some.EqualTo("line from Docs"));
    }
}
