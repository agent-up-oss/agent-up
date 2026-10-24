using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.Services;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Headless.NUnit;

namespace AgentUp.Desktop.Tests.Features.Validation.Headless;

public sealed class DesktopValidationLaunchServiceTests
{
    [AvaloniaTest]
    public async Task RunIfRequestedAsync_returnsNullWhenValidateEnvIsUnset()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        using var surface = new Env(DesktopValidationKeys.Surface, null);
        using var output = new Env(DesktopValidationKeys.Output, null);
        var (service, http) = Service(app, []);
        using var client = http;

        var code = await service.RunIfRequestedAsync(app.Window, app.Window.DataContext!, CancellationToken.None);

        Assert.That(code, Is.Null);
    }

    [AvaloniaTest]
    public async Task RunIfRequestedAsync_replaysWhenValidateEnvIsSet()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var root = Path.Join(Path.GetTempPath(), "desktop-validation", Guid.NewGuid().ToString("N"));
        using var surface = new Env(DesktopValidationKeys.Surface, DesktopValidationKeys.DesktopValue);
        using var output = new Env(DesktopValidationKeys.Output, root);
        using var flow = new Env(DesktopValidationKeys.Flow, "open-git");
        var (service, http) = Service(app, [OpenGitFlow()]);
        using var client = http;

        var code = await service.RunIfRequestedAsync(app.Window, app.Window.DataContext!, CancellationToken.None);

        Assert.That(code, Is.EqualTo(0));
        Assert.That(File.Exists(Path.Join(root, "open-git", "report.json")), Is.True);
    }

    [AvaloniaTest]
    public async Task RunAsync_returnsZeroWhenNoDesktopFlowsExist()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var (service, http) = Service(app, []);
        using var client = http;

        var code = await service.RunAsync(
            app.Window,
            app.Window.DataContext!,
            new DesktopValidationLaunchRequest("/tmp/desktop-validation-empty", null, null, true),
            CancellationToken.None);

        Assert.That(code, Is.EqualTo(0));
    }

    [AvaloniaTest]
    public async Task RunAsync_returnsFailureWhenAListedFlowFails()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var (service, http) = Service(app, [BrokenFlow()]);
        using var client = http;

        var code = await service.RunAsync(
            app.Window,
            app.Window.DataContext!,
            new DesktopValidationLaunchRequest("/tmp/desktop-validation-fail", null, DesktopDomain.WorkspaceId, true),
            CancellationToken.None);

        Assert.That(code, Is.EqualTo(1));
    }

    [AvaloniaTest]
    public async Task RunAsync_copiesTheServerLogIntoTheFlowDirectory()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var root = Path.Join(Path.GetTempPath(), "desktop-validation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var log = Path.Join(root, "server.log");
        await File.WriteAllTextAsync(log, "ready");
        using var serverLog = new Env(DesktopValidationKeys.ServerLog, log);
        var (service, http) = Service(app, [OpenGitFlow()]);
        using var client = http;

        var code = await service.RunAsync(
            app.Window,
            app.Window.DataContext!,
            new DesktopValidationLaunchRequest(root, "open-git", null, true),
            CancellationToken.None);

        Assert.That(code, Is.EqualTo(0));
        Assert.That(await File.ReadAllTextAsync(Path.Join(root, "open-git", "server.log")), Is.EqualTo("ready"));
    }

    private static (DesktopValidationLaunchService Service, HttpClient Http) Service(
        AppDriver app,
        IReadOnlyList<ValidationFlowDto> flows)
    {
        var http = new HttpClient(new ListHandler(flows)) { BaseAddress = new Uri("http://server/") };
        var host = new AvaloniaValidationReplayHost(
            new DesktopVisualTreeLocator(),
            new DesktopControlInput(),
            new DesktopWindowCapture(),
            new DesktopSurfaceNavigator(),
            conditionAttempts: 2);
        host.Attach(app.Window, app.Window.DataContext!);
        var artifacts = new ValidationArtifactStore();
        var client = new ValidationFlowApiClient(http);
        return (new DesktopValidationLaunchService(
            new ValidationFlowReplayService(
                client,
                new BrowserInteractionController(),
                host,
                artifacts,
                skipCosmeticStaging: true),
            client,
            artifacts,
            host,
            new DesktopValidationEnvironment()), http);
    }

    private static ValidationFlowDto OpenGitFlow()
        => new(
            "open-git",
            DesktopDomain.WorkspaceId,
            DesktopValidationKeys.ApplicationName,
            "Open Git",
            "Git",
            DesktopNamedRoutes.WorkspaceGit,
            [new ValidationAssertionDto(ValidationExpectationDto.Visible, "", DesktopDomain.Target().WithTestId(DesktopAutomationIds.GitPanel).Build())],
            [],
            1,
            ValidationSurfaceDto.Desktop);

    private static ValidationFlowDto BrokenFlow()
        => new(
            "broken",
            DesktopDomain.WorkspaceId,
            DesktopValidationKeys.ApplicationName,
            "Broken",
            "Missing target",
            DesktopNamedRoutes.WorkspaceGit,
            [new ValidationAssertionDto(ValidationExpectationDto.Visible, "", DesktopDomain.Target().WithTestId("does-not-exist").Build())],
            [],
            1,
            ValidationSurfaceDto.Desktop);

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

    private sealed class Env : IDisposable
    {
        private readonly string _key;
        private readonly string? _previous;

        public Env(string key, string? value)
        {
            _key = key;
            _previous = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_key, _previous);
    }
}
