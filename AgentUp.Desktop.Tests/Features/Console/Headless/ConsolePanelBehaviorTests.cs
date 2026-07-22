using Avalonia.Headless.NUnit;
using Avalonia.Controls;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
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

    [Test]
    public void Html_encodesLinesAsPreformattedTerminalPage()
    {
        var lines = new[] { "$ cargo run", "<error> & 'warning'", "done" };

        var html = MainWindow.BuildConsoleHtml(lines);

        Assert.That(html, Does.Contain("$ cargo run"));
        Assert.That(html, Does.Contain("&lt;error&gt; &amp; 'warning'"));
        Assert.That(html, Does.Contain("done"));
        Assert.That(html, Does.Contain("<textarea"));
        Assert.That(html, Does.Contain("#07110f"));
    }

    [Test]
    public void Html_stripsAnsiEscapeCodes()
    {
        var lines = new[] { "\x1B[32mgreen text\x1B[0m", "plain" };

        var html = MainWindow.BuildConsoleHtml(lines);

        Assert.That(html, Does.Contain("green text"));
        Assert.That(html, Does.Not.Contain("[32m"));
        Assert.That(html.IndexOf('\x1B'), Is.EqualTo(-1), "HTML should not contain ESC characters");
    }

    [AvaloniaTest]
    public async Task Panel_capsConsoleOutput_atMaxLines()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var tooManyLines = Enumerable.Range(0, ConsoleViewModel.MaxLines + 1)
            .Select(i => $"line {i}")
            .ToList();
        var output = WorkspaceFixtures.OutputFor(workspace.Id, workspace.Applications[0].Name, tooManyLines);

        var app = await AppDriver.LaunchWithWorkspacesAndOutputAsync([workspace], output);

        Assert.That(app.Content.ConsoleLines, Has.Count.EqualTo(ConsoleViewModel.MaxLines));
        Assert.That(app.Content.ConsoleLines[^1], Is.EqualTo($"line {ConsoleViewModel.MaxLines}"));
    }

    [AvaloniaTest]
    public async Task Panel_showsTruncationNotice_whenOutputExceedsMaxLines()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var tooManyLines = Enumerable.Range(0, ConsoleViewModel.MaxLines + 1)
            .Select(i => $"line {i}")
            .ToList();
        var output = WorkspaceFixtures.OutputFor(workspace.Id, workspace.Applications[0].Name, tooManyLines);

        var app = await AppDriver.LaunchWithWorkspacesAndOutputAsync([workspace], output);

        Assert.That(app.Content.ConsoleWasTruncated, Is.True);
    }

    [AvaloniaTest]
    public async Task Panel_doesNotShowTruncationNotice_whenOutputFitsWithinMaxLines()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var output = WorkspaceFixtures.OutputFor(workspace.Id, workspace.Applications[0].Name, ["line 1", "line 2"]);

        var app = await AppDriver.LaunchWithWorkspacesAndOutputAsync([workspace], output);

        Assert.That(app.Content.ConsoleWasTruncated, Is.False);
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

    [AvaloniaTest]
    public async Task Panel_showsHasHiddenLines_whenOutputExceedsDefaultDisplayLines()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var manyLines = Enumerable.Range(0, ConsoleViewModel.DefaultDisplayLines + 1)
            .Select(i => $"line {i}")
            .ToList();
        var output = WorkspaceFixtures.OutputFor(workspace.Id, workspace.Applications[0].Name, manyLines);

        var app = await AppDriver.LaunchWithWorkspacesAndOutputAsync([workspace], output);

        Assert.That(app.Content.ConsoleHasHiddenLines, Is.True);
    }

    [AvaloniaTest]
    public async Task Panel_doesNotShowHasHiddenLines_whenOutputFitsWithinDefaultDisplayLines()
    {
        var workspace = WorkspaceFixtures.WithApplications();
        var output = WorkspaceFixtures.OutputFor(workspace.Id, workspace.Applications[0].Name, ["line 1", "line 2"]);

        var app = await AppDriver.LaunchWithWorkspacesAndOutputAsync([workspace], output);

        Assert.That(app.Content.ConsoleHasHiddenLines, Is.False);
    }
}
