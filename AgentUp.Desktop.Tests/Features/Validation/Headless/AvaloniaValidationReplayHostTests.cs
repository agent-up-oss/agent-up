using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Validation.Headless;

public sealed class AvaloniaValidationReplayHostTests
{
    [AvaloniaTest]
    public async Task Execute_navigatesPressesAndRejectsUnknownActions()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var host = CreateHost();
        host.Attach(app.Window, app.Window.DataContext!);

        var navigated = await host.ExecuteAsync(
            new ValidationStepDto("go", "Overview", ValidationActionDto.Navigate, Value: DesktopNamedRoutes.WorkspaceOverview),
            CancellationToken.None);
        var pressed = await host.ExecuteAsync(
            new ValidationStepDto("enter", "Press enter", ValidationActionDto.Press, Value: null),
            CancellationToken.None);
        var unknown = await host.ExecuteAsync(
            new ValidationStepDto("nope", "Unknown", (ValidationActionDto)42),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(navigated, Is.Null);
            Assert.That(((MainViewModel)app.Window.DataContext!).SelectedShellTab, Is.EqualTo(WorkspaceShellTab.Overview));
            Assert.That(pressed, Is.Null);
            Assert.That(unknown, Does.Contain("Unknown Desktop validation action"));
        });
    }

    [AvaloniaTest]
    public async Task Evaluate_coversMissingTargetsTitleMismatchAndRunningState()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync([DesktopDomain.WorkspaceWithApplications().Build()]);
        var host = CreateHost();
        host.Attach(app.Window, app.Window.DataContext!);
        await host.PrepareAsync(GitFlow(), CancellationToken.None);

        var enabled = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Enabled, "true", DesktopDomain.Target().WithTestId("missing").Build()),
            CancellationToken.None);
        var visible = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Visible, "", DesktopDomain.Target().WithTestId("missing").Build()),
            CancellationToken.None);
        var text = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Text, "Hello", DesktopDomain.Target().WithTestId("missing").Build()),
            CancellationToken.None);
        var title = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Title, "not-the-title"),
            CancellationToken.None);
        var running = await host.EvaluateAsync(
            new ValidationAssertionDto(
                ValidationExpectationDto.Running,
                "",
                DesktopDomain.Target().Named(DesktopDomain.ApiName).Build()),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(enabled.Passed, Is.False);
            Assert.That(visible.Error, Does.Contain("Candidates:"));
            Assert.That(text.Passed, Is.False);
            Assert.That(title.Passed, Is.False);
            Assert.That(running.Passed, Is.True, running.Error);
        });
    }

    [AvaloniaTest]
    public async Task Evaluate_readsWorkspaceStateFromTheSelectedRow()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var host = CreateHost();
        host.Attach(app.Window, app.Window.DataContext!);

        var byId = await host.EvaluateAsync(
            new ValidationAssertionDto(
                ValidationExpectationDto.State,
                DesktopDomain.RunningState,
                DesktopDomain.Target().WithTestId(DesktopDomain.WorkspaceId).Build()),
            CancellationToken.None);
        var unknown = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.State, "Paused"),
            CancellationToken.None);
        var missingId = await host.EvaluateAsync(
            new ValidationAssertionDto(
                ValidationExpectationDto.State,
                DesktopDomain.RunningState,
                DesktopDomain.Target().WithTestId("missing-workspace").Build()),
            CancellationToken.None);
        var titleContains = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Title, "agent-up"),
            CancellationToken.None);

        Assert.That(byId.Passed, Is.True, byId.Error);
        Assert.That(unknown.Passed, Is.False);
        Assert.That(missingId.Passed, Is.True, missingId.Error);
        Assert.That(titleContains.Passed, Is.True, titleContains.Error);
    }

    [AvaloniaTest]
    public async Task Prepare_andCapture_requireAnAttachedWindow()
    {
        var host = CreateHost();

        var prepare = await host.PrepareAsync(GitFlow(), CancellationToken.None);
        Assert.That(prepare, Does.Contain("not attached to a window"));
        Assert.That(
            async () => await host.CapturePngAsync("/tmp/desktop-validation-unattached.png", CancellationToken.None),
            Throws.InvalidOperationException);
    }

    [AvaloniaTest]
    public async Task CapturePng_writesFromAnAttachedWindow()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var host = CreateHost();
        host.Attach(app.Window, app.Window.DataContext!);
        var path = Path.Join(Path.GetTempPath(), "desktop-validation", Guid.NewGuid().ToString("N"), "window.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await host.CapturePngAsync(path, CancellationToken.None);

        Assert.That(File.Exists(path), Is.True);
    }

    [AvaloniaTest]
    public async Task Evaluate_rejectsUnknownExpectationKinds()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var host = CreateHost();
        host.Attach(app.Window, app.Window.DataContext!);

        var result = await host.EvaluateAsync(
            new ValidationAssertionDto((ValidationExpectationDto)99, "x"),
            CancellationToken.None);

        Assert.That(result.Passed, Is.False);
        Assert.That(result.Error, Does.Contain("Unknown Desktop expectation"));
    }

    [AvaloniaTest]
    public async Task Execute_clicksAndFillsResolvedControls()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var host = CreateHost();
        host.Attach(app.Window, app.Window.DataContext!);
        Assert.That(await host.PrepareAsync(GitFlow(), CancellationToken.None), Is.Null);

        var clicked = await host.ExecuteAsync(
            new ValidationStepDto(
                "open-history",
                "Open history",
                ValidationActionDto.Click,
                DesktopDomain.Target().WithTestId(DesktopAutomationIds.GitHistory).Build()),
            CancellationToken.None);
        var filled = await host.ExecuteAsync(
            new ValidationStepDto(
                "type-message",
                "Type a commit message",
                ValidationActionDto.Fill,
                DesktopDomain.Target().WithTestId(DesktopAutomationIds.GitCommitMessage).Build(),
                "feat(Git): cover"),
            CancellationToken.None);
        var absent = await host.EvaluateAsync(
            new ValidationAssertionDto(
                ValidationExpectationDto.Absent,
                "",
                DesktopDomain.Target().WithTestId(DesktopAutomationIds.GitHistoryBack).Build()),
            CancellationToken.None);

        Assert.That(clicked, Is.Null);
        Assert.That(filled, Is.Null);
        Assert.That(absent.Passed, Is.False);
    }

    private static AvaloniaValidationReplayHost CreateHost()
        => new(
            new DesktopVisualTreeLocator(),
            new DesktopControlInput(),
            new DesktopWindowCapture(),
            new DesktopSurfaceNavigator());

    private static ValidationFlowDto GitFlow()
        => new(
            "open-git",
            DesktopDomain.WorkspaceId,
            DesktopValidationKeys.ApplicationName,
            "Open Git",
            "Git",
            DesktopNamedRoutes.WorkspaceGit,
            [],
            [],
            1,
            ValidationSurfaceDto.Desktop);
}
