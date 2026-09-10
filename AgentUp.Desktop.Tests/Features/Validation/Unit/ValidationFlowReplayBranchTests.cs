using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Browser.Resources;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.Services;

namespace AgentUp.Desktop.Tests.Features.Validation.Unit;

// Replay drives a real webview, so every refusal has to come back as a message the panel can show
// rather than an exception. These cover the paths a user actually hits when something is wrong.
public sealed class ValidationFlowReplayBranchTests
{
    [Test]
    public async Task RunAsync_reportsAMissingFlow()
    {
        var (service, http) = Create(status: HttpStatusCode.NotFound);
        using var client = http;
        var reporter = new Reporter();

        var result = await service.RunAsync("ws", "flow", reporter, new Host(), CancellationToken.None);

        AssertFailed(result, reporter, "Validation flow was not found.");
    }

    [Test]
    public async Task RunAsync_reportsAnApplicationWithNoAllocatedPort()
    {
        var (service, http) = Create();
        using var client = http;
        var reporter = new Reporter();

        var result = await service.RunAsync("ws", "flow", reporter, new Host { Origin = null }, CancellationToken.None);

        AssertFailed(result, reporter, "no allocated HTTP port");
    }

    [Test]
    public async Task RunAsync_reportsAViewportItCouldNotOpen()
    {
        var (service, http) = Create();
        using var client = http;
        var reporter = new Reporter();

        var result = await service.RunAsync("ws", "flow", reporter, new Host { Prepared = false }, CancellationToken.None);

        AssertFailed(result, reporter, "Could not open the application browser tab");
    }

    [Test]
    public async Task RunAsync_stopsAtTheStartingPageWhenItsExpectationIsNeverMet()
    {
        var (service, http) = Create();
        using var client = http;
        var reporter = new Reporter();
        var host = new Host { Eval = script => script == BrowserScripts.CheckText("Home") ? "false" : null };

        var result = await service.RunAsync("ws", "flow", reporter, host, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("did not appear in time"));
            // The starting stage is reported as failed, and no step stage is ever begun.
            Assert.That(reporter.FailedStages, Does.Contain("__initial__"));
            Assert.That(reporter.StageIds, Does.Not.Contain("settings"));
        });
    }

    [Test]
    public async Task RunAsync_stopsAtTheStepWhoseExpectationIsNeverMet()
    {
        var (service, http) = Create();
        using var client = http;
        var reporter = new Reporter();
        var host = new Host { Eval = script => script == BrowserScripts.CheckText("Settings") ? "false" : null };

        var result = await service.RunAsync("ws", "flow", reporter, host, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.StepId, Is.EqualTo("settings"));
            Assert.That(reporter.FailedStages, Does.Contain("settings"));
        });
    }

    [Test]
    public async Task RunAsync_surfacesAnErrorTheBrowserScriptItselfReports()
    {
        var (service, http) = Create();
        using var client = http;
        var reporter = new Reporter();
        var host = new Host
        {
            Eval = script => script.Contains("__agentUpMouse") ? """{"error":"Element not found: #settings-tab"}""" : null
        };

        var result = await service.RunAsync("ws", "flow", reporter, host, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Element not found"));
            Assert.That(result.StepId, Is.EqualTo("settings"));
        });
    }

    [Test]
    public async Task RunAsync_treatsAnEmptyScriptResultAsAFailure()
    {
        var (service, http) = Create();
        using var client = http;
        var reporter = new Reporter();
        var host = new Host { Eval = script => script.Contains("__agentUpMouse") ? "" : null };

        var result = await service.RunAsync("ws", "flow", reporter, host, CancellationToken.None);

        Assert.That(result.Message, Does.Contain("returned no result"));
    }

    [Test]
    public async Task RunAsync_refusesAnInteractiveStepThatLostItsSelector()
    {
        var (service, http) = Create(steps: """
            {"id":"orphan","description":"Click nothing","action":"Click","expectations":[{"kind":"Text","value":"Settings"}]}
            """);
        using var client = http;
        var reporter = new Reporter();

        var result = await service.RunAsync("ws", "flow", reporter, new Host(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("selector fallback"));
            Assert.That(result.StepId, Is.EqualTo("orphan"));
        });
    }

    [Test]
    public async Task RunAsync_navigatesRelativeToTheApplicationOrigin()
    {
        var (service, http) = Create(steps: """
            {"id":"go","description":"Open settings","action":"Navigate","value":"/settings","expectations":[{"kind":"Text","value":"Settings"}]}
            """);
        using var client = http;
        var reporter = new Reporter();
        var host = new Host();

        var result = await service.RunAsync("ws", "flow", reporter, host, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(host.Navigations, Does.Contain("http://127.0.0.1:5173/settings"));
        });
    }

    [Test]
    public async Task RunAsync_fillsThroughTheStagedAnimationAndClearsThePing()
    {
        var (service, http) = Create(steps: """
            {"id":"email","description":"Type the email","action":"Fill","target":{"selector":"#email"},"value":"a@b.test","expectations":[{"kind":"Text","value":"Settings"}]}
            """);
        using var client = http;
        var reporter = new Reporter();
        var host = new Host();

        var result = await service.RunAsync("ws", "flow", reporter, host, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(host.Scripts.Any(x => x.Contains("__agentUpMouse")), Is.True);
            Assert.That(host.Scripts.Any(x => x.Contains("a@b.test")), Is.True);
            Assert.That(host.Scripts, Does.Contain(BrowserScripts.RemoveAttentionPing()));
        });
    }

    [Test]
    public async Task RunAsync_pressesAKeyWithoutNeedingATarget()
    {
        var (service, http) = Create(steps: """
            {"id":"submit","description":"Press enter","action":"Press","value":"Enter","expectations":[{"kind":"Text","value":"Settings"}]}
            """);
        using var client = http;
        var reporter = new Reporter();
        var host = new Host();

        var result = await service.RunAsync("ws", "flow", reporter, host, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(host.Scripts, Does.Contain(BrowserScripts.Press("Enter")));
        });
    }

    [Test]
    public async Task RunAsync_acceptsAUrlExpectationByPathOrByWholeUrl()
    {
        foreach (var expected in new[] { "/settings", "http://127.0.0.1:5173/settings" })
        {
            var (service, http) = Create(initial: $$"""{"kind":"Url","value":"{{expected}}"}""", steps: null);
            using var client = http;
            var host = new Host
            {
                Eval = script => script == BrowserScripts.GetUrl ? "\"http://127.0.0.1:5173/settings\"" : null
            };

            var result = await service.RunAsync("ws", "flow", new Reporter(), host, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, $"{expected}: {result.Message}");
        }
    }

    [Test]
    public async Task RunAsync_reportsAUrlOrTitleThatNeverArrives()
    {
        var (urlService, urlHttp) = Create(initial: """{"kind":"Url","value":"/never"}""", steps: null);
        using var urlClient = urlHttp;
        var urlResult = await urlService.RunAsync("ws", "flow", new Reporter(),
            new Host { Eval = script => script == BrowserScripts.GetUrl ? "\"http://127.0.0.1:5173/\"" : null },
            CancellationToken.None);

        var (titleService, titleHttp) = Create(initial: """{"kind":"Title","value":"Nope"}""", steps: null);
        using var titleClient = titleHttp;
        var titleResult = await titleService.RunAsync("ws", "flow", new Reporter(),
            new Host { Eval = script => script == BrowserScripts.GetTitle ? "\"Other\"" : null },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(urlResult.Message, Does.Contain("Expected url '/never'"));
            Assert.That(titleResult.Message, Does.Contain("Expected title 'Nope'"));
        });
    }

    [Test]
    public async Task RunAsync_checksVisibilityThroughTheSelector_andRefusesOneWithoutIt()
    {
        var (withSelector, http1) = Create(initial: """{"kind":"Visible","value":"ignored","target":{"selector":"#cart"}}""", steps: null);
        using var client1 = http1;
        var host = new Host { Eval = script => script == BrowserScripts.CheckSelector("#cart") ? "true" : null };
        var visible = await withSelector.RunAsync("ws", "flow", new Reporter(), host, CancellationToken.None);

        var (withoutSelector, http2) = Create(initial: """{"kind":"Visible","value":"ignored"}""", steps: null);
        using var client2 = http2;
        var refused = await withoutSelector.RunAsync("ws", "flow", new Reporter(), new Host(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(visible.Succeeded, Is.True, visible.Message);
            Assert.That(refused.Message, Does.Contain("selector fallback for visibility"));
        });
    }

    private static void AssertFailed(ValidationReplayResult result, Reporter reporter, string expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain(expected));
            Assert.That(reporter.CompletedFlowPassed, Is.False);
        });
    }

    private static (ValidationFlowReplayService Service, HttpClient Http) Create(
        HttpStatusCode status = HttpStatusCode.OK,
        string? initial = null,
        string? steps = "{\"id\":\"settings\",\"description\":\"Open settings tab\",\"action\":\"Click\",\"target\":{\"selector\":\"#settings-tab\"},\"expectations\":[{\"kind\":\"Text\",\"value\":\"Settings\"}]}")
    {
        var json = $$"""
            {
              "id":"flow","workspaceId":"ws","application":"shop","name":"Tabs work",
              "description":"User can switch tabs","initialPath":"/",
              "initialExpectations":[{{initial ?? """{"kind":"Text","value":"Home"}"""}}],
              "steps":[{{steps ?? ""}}],
              "version":1
            }
            """;
        var http = new HttpClient(new Handler(status, json)) { BaseAddress = new Uri("http://server/") };
        return (new ValidationFlowReplayService(new ValidationFlowApiClient(http), new BrowserInteractionController()), http);
    }

    private sealed class Handler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private sealed class Host : IValidationReplayHost
    {
        public string? Origin { get; init; } = "http://127.0.0.1:5173";
        public bool Prepared { get; init; } = true;

        // Returns null to fall through to the permissive default, so each test only has to
        // describe the one script whose answer it cares about.
        public Func<string, string?>? Eval { get; init; }

        public List<string> Scripts { get; } = [];
        public List<string> Navigations { get; } = [];

        public string? ResolveApplicationOrigin(string workspaceId, string application) => Origin;

        public Task<bool> PrepareViewportAsync(string workspaceId, string application, string url, CancellationToken cancellationToken) =>
            Task.FromResult(Prepared);

        public Task<string?> EvalAsync(string workspaceId, string script, CancellationToken cancellationToken)
        {
            Scripts.Add(script);
            return Task.FromResult(Eval?.Invoke(script) ?? Default(script));
        }

        public Task NavigateAsync(string workspaceId, string url, CancellationToken cancellationToken)
        {
            Navigations.Add(url);
            return Task.CompletedTask;
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;

        private static string Default(string script)
        {
            if (script == BrowserScripts.CheckNavigation) return "\"complete\"";
            if (script == BrowserScripts.GetUrl) return "\"http://127.0.0.1:5173/\"";
            if (script == BrowserScripts.GetTitle) return "\"Tabs work\"";
            // Predicate scripts answer with a bare boolean; CheckText also opens with '(', so
            // match both by shape rather than by that first character.
            if (script.StartsWith("(document.body", StringComparison.Ordinal)
                || script.StartsWith("!!document.querySelector", StringComparison.Ordinal))
                return "true";
            return """{"ok":true}""";
        }
    }

    private sealed class Reporter : IValidationReplayReporter
    {
        public List<string> StageIds { get; } = [];
        public List<string> FailedStages { get; } = [];
        public bool? CompletedFlowPassed { get; private set; }

        public void BeginFlow() => CompletedFlowPassed = null;

        public void BeginStage(string stageId) => StageIds.Add(stageId);

        public void BeginCheck(string stageId, int checkIndex)
        {
            // Progress only; the assertions here read the terminal states instead.
        }

        public void CompleteCheck(string stageId, int checkIndex, bool passed, string? error = null)
        {
            // Progress only; the assertions here read the terminal states instead.
        }

        public void CompleteStage(string stageId, bool passed, string? error = null)
        {
            if (!passed) FailedStages.Add(stageId);
        }

        public void CompleteFlow(bool passed, string? message = null) => CompletedFlowPassed = passed;
    }
}
