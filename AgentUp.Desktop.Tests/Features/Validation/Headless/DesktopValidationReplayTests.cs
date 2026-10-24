using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Headless.NUnit;

namespace AgentUp.Desktop.Tests.Features.Validation.Headless;

public sealed class DesktopValidationReplayTests
{
    [AvaloniaTest]
    public async Task Replay_navigatesTheGitRouteAndWritesArtifacts()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var root = ArtifactRoot();
        var host = CreateHost();
        host.Attach(app.Window, app.Window.DataContext!);
        var artifacts = new ValidationArtifactStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var replay = Replay(host, artifacts, new ValidationFlowApiClient(http));

        var result = await replay.RunFlowAsync(
            OpenGitFlow(DesktopDomain.WorkspaceId),
            new SilentValidationReporter(),
            root,
            CancellationToken.None);

        var directory = artifacts.FlowDirectory(root, "open-git");
        var report = await File.ReadAllTextAsync(Path.Join(directory, "report.json"));
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(File.Exists(Path.Join(directory, "00-initial.png")), Is.True);
            Assert.That(report, Does.Contain("\"passed\": true"));
            Assert.That(report, Does.Contain("\"surface\": \"Desktop\""));
        });
    }

    [AvaloniaTest]
    public async Task Replay_clicksHistoryAndRecordsTheResolvedTarget()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        var root = ArtifactRoot();
        var host = CreateHost();
        host.Attach(app.Window, viewModel);
        var artifacts = new ValidationArtifactStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var replay = Replay(host, artifacts, new ValidationFlowApiClient(http));

        var result = await replay.RunFlowAsync(
            OpenGitHistoryFlow(DesktopDomain.WorkspaceId),
            new SilentValidationReporter(),
            root,
            CancellationToken.None);

        var report = JsonSerializer.Deserialize<ValidationRunReportDto>(
            await File.ReadAllTextAsync(Path.Join(artifacts.FlowDirectory(root, "open-history"), "report.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(viewModel.Git.IsHistoryOpen, Is.True);
            Assert.That(report!.Steps, Has.Count.EqualTo(2));
            Assert.That(report.Steps[1].Resolved?.AutomationId, Is.EqualTo(DesktopAutomationIds.GitHistory));
            Assert.That(File.Exists(Path.Join(artifacts.FlowDirectory(root, "open-history"), "01-open-history.png")), Is.True);
        });
    }

    [AvaloniaTest]
    public async Task Evaluate_coversDesktopExpectationKinds()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var host = CreateHost();
        host.Attach(app.Window, app.Window.DataContext!);
        Assert.That(await host.PrepareAsync(OpenGitFlow(DesktopDomain.WorkspaceId), CancellationToken.None), Is.Null);

        var visible = await host.EvaluateAsync(Visible(DesktopAutomationIds.GitPanel), CancellationToken.None);
        var enabled = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Enabled, "true", Target(DesktopAutomationIds.GitHistory)),
            CancellationToken.None);
        var absent = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Absent, "", Target(DesktopAutomationIds.GitHistoryBack)),
            CancellationToken.None);
        var title = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Title, "Agent-Up"),
            CancellationToken.None);
        var state = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.State, DesktopDomain.RunningState),
            CancellationToken.None);
        var commit = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Enabled, "false", Target(DesktopAutomationIds.GitCommit)),
            CancellationToken.None);
        var text = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Text, "forge capability", Target(DesktopAutomationIds.GitHostingTitle)),
            CancellationToken.None);
        var url = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Url, "/git"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(visible.Passed, Is.True, visible.Error);
            Assert.That(enabled.Passed, Is.True, enabled.Error);
            Assert.That(absent.Passed, Is.True, absent.Error);
            Assert.That(title.Passed, Is.True, title.Error);
            Assert.That(state.Passed, Is.True, state.Error);
            Assert.That(commit.Passed, Is.True, commit.Error);
            Assert.That(text.Passed, Is.True, text.Error);
            Assert.That(url.Passed, Is.False);
            Assert.That(url.Error, Does.Contain("invalid on a Desktop surface"));
        });
    }

    [AvaloniaTest]
    public async Task Execute_listsCandidatesWhenATestIdIsMissing()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var host = CreateHost();
        host.Attach(app.Window, app.Window.DataContext!);
        Assert.That(await host.PrepareAsync(OpenGitFlow(DesktopDomain.WorkspaceId), CancellationToken.None), Is.Null);

        var error = await host.ExecuteAsync(
            new ValidationStepDto("missing", "Missing", ValidationActionDto.Click, Target("git.does-not-exist")),
            CancellationToken.None);

        Assert.That(error, Does.Contain("Candidates:"));
        Assert.That(host.LastCandidates, Is.Not.Empty);
    }

    [AvaloniaTest]
    public async Task Execute_fillsTheCommitMessageBox()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var host = CreateHost();
        host.Attach(app.Window, app.Window.DataContext!);
        Assert.That(await host.PrepareAsync(OpenGitFlow(DesktopDomain.WorkspaceId), CancellationToken.None), Is.Null);

        var error = await host.ExecuteAsync(
            new ValidationStepDto(
                "type-message",
                "Type a commit message",
                ValidationActionDto.Fill,
                Target(DesktopAutomationIds.GitCommitMessage),
                "feat(Git): cover"),
            CancellationToken.None);
        var text = await host.EvaluateAsync(
            new ValidationAssertionDto(ValidationExpectationDto.Text, "feat(Git): cover", Target(DesktopAutomationIds.GitCommitMessage)),
            CancellationToken.None);

        Assert.That(error, Is.Null);
        Assert.That(text.Passed, Is.True, text.Error);
    }

    [AvaloniaTest]
    public async Task LaunchService_returnsFailureWhenTheRequestedFlowIsMissing()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        using var http = new HttpClient(new ListHandler([])) { BaseAddress = new Uri("http://server/") };
        var host = CreateHost();
        var artifacts = new ValidationArtifactStore();
        var client = new ValidationFlowApiClient(http);
        var service = new DesktopValidationLaunchService(
            Replay(host, artifacts, client),
            client,
            artifacts,
            host,
            new DesktopValidationEnvironment());

        var code = await service.RunAsync(
            app.Window,
            app.Window.DataContext!,
            new DesktopValidationLaunchRequest("/tmp/desktop-validation-missing", "missing", DesktopDomain.WorkspaceId, true),
            CancellationToken.None);

        Assert.That(code, Is.EqualTo(1));
    }

    [AvaloniaTest]
    public async Task LaunchService_replaysListedDesktopFlowsAgainstTheWindow()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var flow = OpenGitFlow(DesktopDomain.WorkspaceId);
        using var http = new HttpClient(new ListHandler([flow])) { BaseAddress = new Uri("http://server/") };
        var host = CreateHost();
        var artifacts = new ValidationArtifactStore();
        var client = new ValidationFlowApiClient(http);
        var service = new DesktopValidationLaunchService(
            Replay(host, artifacts, client),
            client,
            artifacts,
            host,
            new DesktopValidationEnvironment());
        var root = ArtifactRoot();

        var code = await service.RunAsync(
            app.Window,
            app.Window.DataContext!,
            new DesktopValidationLaunchRequest(root, flow.Id, DesktopDomain.WorkspaceId, true),
            CancellationToken.None);

        Assert.That(code, Is.EqualTo(0));
        Assert.That(File.Exists(Path.Join(artifacts.FlowDirectory(root, flow.Id), "report.json")), Is.True);
    }

    private static AvaloniaValidationReplayHost CreateHost()
        => new(
            new DesktopVisualTreeLocator(),
            new DesktopControlInput(),
            new DesktopWindowCapture(),
            new DesktopSurfaceNavigator());

    private static ValidationFlowReplayService Replay(
        AvaloniaValidationReplayHost host,
        ValidationArtifactStore artifacts,
        ValidationFlowApiClient client)
        => new(
            client,
            new BrowserInteractionController(),
            host,
            artifacts,
            skipCosmeticStaging: true);

    private static string ArtifactRoot()
        => Path.Join(Path.GetTempPath(), "desktop-validation", Guid.NewGuid().ToString("N"));

    private static ValidationFlowDto OpenGitFlow(string workspaceId)
        => new(
            "open-git",
            workspaceId,
            DesktopValidationKeys.ApplicationName,
            "Open Git",
            "The Git tab is reachable from a named Desktop route.",
            DesktopNamedRoutes.WorkspaceGit,
            [Visible(DesktopAutomationIds.GitPanel)],
            [],
            1,
            ValidationSurfaceDto.Desktop);

    private static ValidationFlowDto OpenGitHistoryFlow(string workspaceId)
        => new(
            "open-history",
            workspaceId,
            DesktopValidationKeys.ApplicationName,
            "Open Git history",
            "History opens from the Git tab.",
            DesktopNamedRoutes.WorkspaceGit,
            [
                Visible(DesktopAutomationIds.GitPanel),
                new ValidationAssertionDto(ValidationExpectationDto.Enabled, "true", Target(DesktopAutomationIds.GitHistory)),
                new ValidationAssertionDto(ValidationExpectationDto.Absent, "", Target(DesktopAutomationIds.GitHistoryBack))
            ],
            [
                new ValidationStepDto(
                    "open-history",
                    "Open history",
                    ValidationActionDto.Click,
                    Target(DesktopAutomationIds.GitHistory),
                    Expectations: [Visible(DesktopAutomationIds.GitHistoryBack)])
            ],
            1,
            ValidationSurfaceDto.Desktop);

    private static ValidationAssertionDto Visible(string testId)
        => new(ValidationExpectationDto.Visible, "", Target(testId));

    private static ValidationTargetDto Target(string testId) => new(TestId: testId);

    private sealed class ListHandler(IReadOnlyList<ValidationFlowDto> flows) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(
                flows,
                new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });
            return Task.FromResult(CreateResponse(json));
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
        private static HttpResponseMessage CreateResponse(string body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
    }
}
