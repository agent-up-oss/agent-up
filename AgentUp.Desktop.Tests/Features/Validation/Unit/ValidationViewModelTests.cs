using System.Net;
using System.Reactive.Linq;
using System.Text;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.Services;
using AgentUp.Desktop.Features.Validation.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Validation.Unit;

public sealed class ValidationViewModelTests
{
    [Test]
    public async Task Load_exposes_selected_application_flows_and_play_runs_local_replay()
    {
        var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://server/") };
        var api = new ValidationFlowApiClient(http);
        var replay = new ValidationFlowReplayService(api, new AgentUp.Desktop.Features.Browser.Controllers.BrowserInteractionController());
        replay.Connect(new FakeReplayHost());
        var vm = new ValidationViewModel(api, replay);
        await vm.LoadAsync("ws", "shop");
        Assert.That(vm.Flows.Single().Name, Is.EqualTo("Checkout works"));
        await vm.Flows.Single().RunCommand.Execute().FirstAsync();
        var flow = vm.Flows.Single();
        Assert.Multiple(() =>
        {
            Assert.That(handler.LastGetPath, Does.EndWith("/validation-flows/flow"));
            Assert.That(flow.IsExpanded, Is.True);
            Assert.That(flow.RunState, Is.EqualTo(AgentUp.Desktop.Features.Validation.Models.ValidationRunState.Passed));
            Assert.That(flow.ResultMessage, Does.Contain("passed"));
            Assert.That(flow.Stages.Single().State, Is.EqualTo(AgentUp.Desktop.Features.Validation.Models.ValidationRunState.Passed));
            Assert.That(flow.Stages.Single().Checks.Single().State, Is.EqualTo(AgentUp.Desktop.Features.Validation.Models.ValidationRunState.Passed));
        });
    }

    [Test]
    public async Task ToggleCommand_collapsesTheSidebarRail()
    {
        var vm = new ValidationViewModel(
            new ValidationFlowApiClient(new HttpClient { BaseAddress = new Uri("http://server/") }),
            new ValidationFlowReplayService(
                new ValidationFlowApiClient(new HttpClient { BaseAddress = new Uri("http://server/") }),
                new AgentUp.Desktop.Features.Browser.Controllers.BrowserInteractionController()));

        Assert.That(vm.IsExpanded, Is.True);
        Assert.That(vm.Width, Is.EqualTo(360));

        await vm.ToggleCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsCollapsed, Is.True);
            Assert.That(vm.IsExpanded, Is.False);
            Assert.That(vm.Width, Is.EqualTo(56));
            Assert.That(vm.ToggleIcon, Is.EqualTo("‹"));
        });
    }

    [Test]
    public void Clear_asksTheUserToSelectAnApplication()
    {
        var vm = new ValidationViewModel(
            new ValidationFlowApiClient(new HttpClient { BaseAddress = new Uri("http://server/") }),
            new ValidationFlowReplayService(
                new ValidationFlowApiClient(new HttpClient { BaseAddress = new Uri("http://server/") }),
                new AgentUp.Desktop.Features.Browser.Controllers.BrowserInteractionController()));

        vm.Clear();

        Assert.That(vm.Status, Is.EqualTo("Select an application to see validation checks."));
        Assert.That(vm.Flows, Is.Empty);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public string? LastGetPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/flow"))
            {
                LastGetPath = request.RequestUri.AbsolutePath;
                const string flow =
                    """
                    {"id":"flow","workspaceId":"ws","application":"shop","name":"Checkout works","description":"Visible outcome","initialPath":"/","initialExpectations":[{"kind":"Text","value":"Cart"}],"steps":[],"version":1}
                    """;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(flow, Encoding.UTF8, "application/json")
                });
            }

            var list = "[{\"id\":\"flow\",\"workspaceId\":\"ws\",\"application\":\"shop\",\"name\":\"Checkout works\",\"description\":\"Visible outcome\",\"initialPath\":\"/\",\"initialExpectations\":[{\"kind\":\"Text\",\"value\":\"Cart\"}],\"steps\":[],\"version\":1}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(list, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeReplayHost : AgentUp.Desktop.Features.Validation.Interfaces.IValidationReplayHost
    {
        public string? ResolveApplicationOrigin(string workspaceId, string application) => "http://127.0.0.1:5173";
        public Task<bool> PrepareViewportAsync(string workspaceId, string application, string url, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<string?> EvalAsync(string workspaceId, string script, CancellationToken cancellationToken)
            => Task.FromResult<string?>(script.Contains("includes") ? "true" : "\"complete\"");
        public Task NavigateAsync(string workspaceId, string url, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
