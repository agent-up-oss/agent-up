using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Browser.Resources;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.Services;

namespace AgentUp.Desktop.Tests.Features.Validation.Unit;

public sealed class ValidationFlowReplayServiceTests
{
    [Test]
    public async Task RunAsync_reports_failure_when_host_is_not_connected()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var service = CreateService(http);
        var reporter = new RecordingReporter();

        var result = await service.RunAsync("ws", "flow", reporter);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("not connected"));
            Assert.That(reporter.CompletedFlowPassed, Is.False);
        });
    }

    [Test]
    public async Task RunAsync_executes_staged_click_and_assertions_in_the_webview()
    {
        var handler = new ReplayHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://server/") };
        var service = CreateService(http);
        var reporter = new RecordingReporter();
        FakeReplayHost.Scripts.Clear();
        service.Connect(new FakeReplayHost());

        var result = await service.RunAsync("ws", "flow", reporter);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(handler.LastPath, Does.EndWith("/validation-flows/flow"));
            Assert.That(FakeReplayHost.Scripts.Any(s => s.Contains("__agentUpMouse")), Is.True);
            Assert.That(FakeReplayHost.Scripts.Any(s => s.Contains("__agentUpClickRing")), Is.True);
            Assert.That(reporter.CompletedFlowPassed, Is.True);
            Assert.That(reporter.StageIds, Does.Contain("settings"));
        });
    }

    private static ValidationFlowReplayService CreateService(HttpClient http) =>
        new(new ValidationFlowApiClient(http), new BrowserInteractionController());

    private sealed class RecordingReporter : IValidationReplayReporter
    {
        public List<string> StageIds { get; } = [];
        public bool? CompletedFlowPassed { get; private set; }

        public void BeginFlow()
        {
        }

        public void BeginStage(string stageId) => StageIds.Add(stageId);

        public void BeginCheck(string stageId, int checkIndex)
        {
        }

        public void CompleteCheck(string stageId, int checkIndex, bool passed, string? error = null)
        {
        }

        public void CompleteStage(string stageId, bool passed, string? error = null)
        {
        }

        public void CompleteFlow(bool passed, string? message = null) => CompletedFlowPassed = passed;
    }

    private sealed class ReplayHandler : HttpMessageHandler
    {
        public string? LastPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            const string json =
                """
                {
                  "id":"flow",
                  "workspaceId":"ws",
                  "application":"shop",
                  "name":"Tabs work",
                  "description":"User can switch tabs",
                  "initialPath":"/",
                  "initialExpectations":[{"kind":"Text","value":"Home"}],
                  "steps":[
                    {
                      "id":"settings",
                      "description":"Open settings tab",
                      "action":"Click",
                      "target":{"selector":"#settings-tab"},
                      "expectations":[{"kind":"Text","value":"Settings"}]
                    }
                  ],
                  "version":1
                }
                """;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeReplayHost : IValidationReplayHost
    {
        internal static readonly List<string> Scripts = [];

        public string? ResolveApplicationOrigin(string workspaceId, string application) => "http://127.0.0.1:5173";

        public Task<bool> PrepareViewportAsync(string workspaceId, string application, string url, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<string?> EvalAsync(string workspaceId, string script, CancellationToken cancellationToken)
        {
            Scripts.Add(script);
            if (script == BrowserScripts.CheckNavigation)
                return Task.FromResult<string?>("\"complete\"");
            if (script == BrowserScripts.CheckText("Home") || script == BrowserScripts.CheckText("Settings"))
                return Task.FromResult<string?>("true");
            if (script.Contains("__agentUpMouse") || script.Contains("__agentUpClickRing") || script.Contains(".click()"))
                return Task.FromResult<string?>("{\"ok\":true}");
            return Task.FromResult<string?>("{\"ok\":true}");
        }

        public Task NavigateAsync(string workspaceId, string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
