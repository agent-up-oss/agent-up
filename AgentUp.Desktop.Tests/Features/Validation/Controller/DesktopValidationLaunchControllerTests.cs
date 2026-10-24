using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Validation.Controllers;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.Services;

namespace AgentUp.Desktop.Tests.Features.Validation.Controller;

public sealed class DesktopValidationLaunchControllerTests
{
    [Test]
    public void IsRequested_isFalseWhenValidateEnvIsUnset()
    {
        using var surface = new Env(DesktopValidationKeys.Surface, null);
        using var output = new Env(DesktopValidationKeys.Output, null);
        using var password = new Env("AGENTUP_ADMIN_PASSWORD", null);
        var host = new RecordingHost();
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var controller = Controller(host, http);

        Assert.That(controller.IsRequested, Is.False);
        Assert.That(controller.ShouldPromptInteractively, Is.True);
        Assert.That(controller.AdminPassword, Is.Null);
    }

    [Test]
    public void ShouldPromptInteractively_isFalseWhenValidateLaunchIsRequested()
    {
        using var surface = new Env(DesktopValidationKeys.Surface, DesktopValidationKeys.DesktopValue);
        using var output = new Env(DesktopValidationKeys.Output, "/tmp/desktop-validation");
        var host = new RecordingHost();
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var controller = Controller(host, http);

        Assert.That(controller.IsRequested, Is.True);
        Assert.That(controller.ShouldPromptInteractively, Is.False);
    }

    [Test]
    public async Task RunIfRequestedAsync_returnsNullWhenValidateEnvIsUnset()
    {
        using var surface = new Env(DesktopValidationKeys.Surface, null);
        using var output = new Env(DesktopValidationKeys.Output, null);
        var host = new RecordingHost();
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var controller = Controller(host, http);

        var code = await controller.RunIfRequestedAsync("window", "view-model", CancellationToken.None);

        Assert.That(code, Is.Null);
    }

    [Test]
    public void AdminPassword_readsTheProcessEnvironment()
    {
        using var password = new Env("AGENTUP_ADMIN_PASSWORD", "secret");
        var host = new RecordingHost();
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var controller = Controller(host, http);

        Assert.That(controller.AdminPassword, Is.EqualTo("secret"));
    }

    [Test]
    public void Attach_forwardsTheWindowToTheHost()
    {
        var host = new RecordingHost();
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var controller = Controller(host, http);

        controller.Attach("window", "view-model");

        Assert.That(host.AttachedWindow, Is.EqualTo("window"));
        Assert.That(host.AttachedViewModel, Is.EqualTo("view-model"));
    }

    private static DesktopValidationLaunchController Controller(IDesktopValidationReplayHost host, HttpClient http)
    {
        var artifacts = new ValidationArtifactStore();
        var environment = new DesktopValidationEnvironment();
        var replay = new ValidationFlowReplayService(
            new ValidationFlowApiClient(http),
            new BrowserInteractionController(),
            host,
            artifacts);
        return new DesktopValidationLaunchController(
            host,
            new DesktopValidationLaunchService(
                replay,
                new ValidationFlowApiClient(http),
                artifacts,
                host,
                environment));
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

    private sealed class RecordingHost : IDesktopValidationReplayHost
    {
        public object? AttachedWindow { get; private set; }
        public object? AttachedViewModel { get; private set; }
        public DesktopResolvedTargetDto? LastResolution => null;
        public IReadOnlyList<DesktopTargetCandidateDto> LastCandidates { get; } = [];

        public void Attach(object window, object viewModel)
        {
            AttachedWindow = window;
            AttachedViewModel = viewModel;
        }

        public Task<string?> PrepareAsync(ValidationFlowDto flow, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);

        public Task<string?> ExecuteAsync(ValidationStepDto step, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);

        public Task<ValidationExpectationReportDto> EvaluateAsync(
            ValidationAssertionDto assertion,
            CancellationToken cancellationToken)
            => Task.FromResult(new ValidationExpectationReportDto("Visible", "", true, "true", null));

        public Task CapturePngAsync(string path, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
