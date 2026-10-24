using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.Services;

namespace AgentUp.Desktop.Tests.Features.Validation.Unit;

public sealed class ValidationFlowReplayDesktopTests
{
    [Test]
    public async Task RunAsync_usesTheDesktopHostForDesktopSurfaceFlows()
    {
        var host = new FakeDesktopHost();
        var artifacts = new MemoryArtifactStore();
        var (service, http) = Create(host, artifacts);
        using var client = http;

        var result = await service.RunAsync("ws", "flow", new Reporter(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(host.Prepared, Is.True);
            Assert.That(host.Executed, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task RunAsync_failsDesktopReplayWhenTheHostIsMissing()
    {
        var (service, http) = Create(host: null, artifacts: null);
        using var client = http;

        var result = await service.RunAsync("ws", "flow", new Reporter(), CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("not attached to a window"));
    }

    [Test]
    public async Task RunFlowAsync_recordsCandidatesWhenADesktopTargetIsMissing()
    {
        var host = new FakeDesktopHost
        {
            ExecuteError = "Target 'missing' was not found. Candidates: git.commit.",
            Candidates = [new DesktopTargetCandidateDto("Button", "git.commit", "GitCommitButton")]
        };
        var artifacts = new MemoryArtifactStore();
        var (service, http) = Create(host, artifacts);
        using var client = http;

        var result = await service.RunFlowAsync(DesktopFlow(), new Reporter(), "/tmp/out", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.StepId, Is.EqualTo("open-git"));
            Assert.That(artifacts.Reports[0].Steps[1].Candidates, Has.Count.EqualTo(1));
            Assert.That(artifacts.Reports[0].Steps[1].Candidates[0].AutomationId, Is.EqualTo("git.commit"));
        });
    }

    [Test]
    public async Task RunAsync_skipsCosmeticStagingWhenAsked()
    {
        var handler = new Handler(HttpStatusCode.OK, ApplicationJson());
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://server/") };
        var host = new ApplicationHost();
        var service = new ValidationFlowReplayService(
            new ValidationFlowApiClient(http),
            new BrowserInteractionController(),
            skipCosmeticStaging: true);
        service.Connect(host);

        var result = await service.RunAsync("ws", "flow", new Reporter(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(host.Scripts.Any(script => script.Contains("getElementById('__agentUpMouse')")), Is.False);
            Assert.That(host.Scripts.Any(script => script.Contains(".click()")), Is.True);
        });
    }

    private static (ValidationFlowReplayService Service, HttpClient Http) Create(
        IDesktopValidationReplayHost? host,
        IValidationArtifactStore? artifacts)
    {
        var json = JsonSerializer.Serialize(DesktopFlow());
        var http = new HttpClient(new Handler(HttpStatusCode.OK, json)) { BaseAddress = new Uri("http://server/") };
        return (new ValidationFlowReplayService(
            new ValidationFlowApiClient(http),
            new BrowserInteractionController(),
            host,
            artifacts,
            skipCosmeticStaging: true), http);
    }

    private static ValidationFlowDto DesktopFlow()
        => new(
            "flow",
            "ws",
            "Desktop",
            "Open Git",
            "Open the Git tab",
            "workspace/git",
            [new ValidationAssertionDto(ValidationExpectationDto.Visible, "", new ValidationTargetDto(TestId: "shell.tab.git"))],
            [
                new ValidationStepDto(
                    "open-git",
                    "Click Git",
                    ValidationActionDto.Click,
                    new ValidationTargetDto(TestId: "shell.tab.git"),
                    Expectations: [new ValidationAssertionDto(ValidationExpectationDto.Visible, "", new ValidationTargetDto(TestId: "git.panel"))])
            ],
            1,
            ValidationSurfaceDto.Desktop);

    private static string ApplicationJson()
        => """
            {
              "id":"flow","workspaceId":"ws","application":"shop","name":"Tabs work",
              "description":"User can switch tabs","initialPath":"/",
              "initialExpectations":[{"kind":"Text","value":"Home"}],
              "steps":[{"id":"settings","description":"Open settings tab","action":"Click","target":{"selector":"#settings-tab"},"expectations":[{"kind":"Text","value":"Settings"}]}],
              "version":1
            }
            """;

    private sealed class Handler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(CreateResponse(status, json));

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
        private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string body) =>
            new(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
    }

    private sealed class FakeDesktopHost : IDesktopValidationReplayHost
    {
        public bool Prepared { get; private set; }
        public List<ValidationStepDto> Executed { get; } = [];
        public string? ExecuteError { get; init; }
        public IReadOnlyList<DesktopTargetCandidateDto> Candidates { get; init; } = [];
        public DesktopResolvedTargetDto? LastResolution { get; private set; }
        public IReadOnlyList<DesktopTargetCandidateDto> LastCandidates { get; private set; } = [];

        public void Attach(object window, object viewModel)
        {
        }

        public Task<string?> PrepareAsync(ValidationFlowDto flow, CancellationToken cancellationToken)
        {
            Prepared = true;
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ExecuteAsync(ValidationStepDto step, CancellationToken cancellationToken)
        {
            Executed.Add(step);
            LastCandidates = Candidates;
            LastResolution = ExecuteError is null
                ? new DesktopResolvedTargetDto("Button", step.Target?.TestId, 0, 0, 10, 10)
                : null;
            return Task.FromResult(ExecuteError);
        }

        public Task<ValidationExpectationReportDto> EvaluateAsync(
            ValidationAssertionDto assertion,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ValidationExpectationReportDto(
                assertion.Kind.ToString(),
                assertion.Value,
                true,
                "true",
                null));
        }

        public Task CapturePngAsync(string path, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class MemoryArtifactStore : IValidationArtifactStore
    {
        public List<ValidationRunReportDto> Reports { get; } = [];

        public string FlowDirectory(string outputRoot, string flowId) => Path.Join(outputRoot, flowId);

        public Task WriteScreenshotAsync(
            string path,
            Func<string, CancellationToken, Task> capture,
            CancellationToken cancellationToken)
            => capture(path, cancellationToken);

        public Task WriteReportAsync(
            string directory,
            ValidationRunReportDto report,
            CancellationToken cancellationToken)
        {
            Reports.Add(report);
            return Task.CompletedTask;
        }

        public Task CopyLogAsync(
            string directory,
            string fileName,
            string? sourcePath,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class ApplicationHost : IValidationReplayHost
    {
        public List<string> Scripts { get; } = [];

        public string? ResolveApplicationOrigin(string workspaceId, string application) => "http://127.0.0.1:5173";

        public Task<bool> PrepareViewportAsync(string workspaceId, string application, string url, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<string?> EvalAsync(string workspaceId, string script, CancellationToken cancellationToken)
        {
            Scripts.Add(script);
            if (script.Contains("readyState"))
                return Task.FromResult<string?>("\"complete\"");
            if (script.StartsWith("(document.body", StringComparison.Ordinal)
                || script.StartsWith("!!document.querySelector", StringComparison.Ordinal))
                return Task.FromResult<string?>("true");
            return Task.FromResult<string?>("{\"ok\":true}");
        }

        public Task NavigateAsync(string workspaceId, string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Reporter : IValidationReplayReporter
    {
        public void BeginFlow()
        {
        }

        public void BeginStage(string stageId)
        {
        }

        public void BeginCheck(string stageId, int checkIndex)
        {
        }

        public void CompleteCheck(string stageId, int checkIndex, bool passed, string? error = null)
        {
        }

        public void CompleteStage(string stageId, bool passed, string? error = null)
        {
        }

        public void CompleteFlow(bool passed, string? message = null)
        {
        }
    }
}
