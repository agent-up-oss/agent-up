using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Validation.Controllers;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.Services;
using AgentUp.Desktop.Features.Validation.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Validation.Controller;

public sealed class ValidationControllerTests{
    [Test]
    public async Task Load_forwards_workspace_and_application_boundary_values()
    {
        var handler = new Handler();
        var api = new ValidationFlowApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://server/") });
        var controller = new ValidationController(new ValidationViewModel(
            api,
            new ValidationFlowReplayService(api, new AgentUp.Desktop.Features.Browser.Controllers.BrowserInteractionController())));
        await controller.LoadAsync("ws one", "web app");
        Assert.That(handler.Query, Is.EqualTo("?application=web%20app"));
    }
    private sealed class Handler : HttpMessageHandler
    {
        public string? Query { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Query = request.RequestUri!.Query; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]", Encoding.UTF8, "application/json") }); }
    }
}
