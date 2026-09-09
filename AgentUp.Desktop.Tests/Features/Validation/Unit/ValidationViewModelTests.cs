using System.Net;
using System.Text;
using System.Reactive.Linq;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.ViewModels;
namespace AgentUp.Desktop.Tests.Features.Validation.Unit;
public sealed class ValidationViewModelTests
{
    [Test]
    public async Task Load_exposes_selected_application_flows_and_play_calls_server()
    {
        var handler = new Handler();
        var vm = new ValidationViewModel(new ValidationFlowApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://server/") }));
        await vm.LoadAsync("ws", "shop");
        Assert.That(vm.Flows.Single().Name, Is.EqualTo("Checkout works"));
        await vm.Flows.Single().RunCommand.Execute().FirstAsync();
        Assert.Multiple(() => { Assert.That(handler.LastMethod, Is.EqualTo(HttpMethod.Post)); Assert.That(handler.LastPath, Does.EndWith("/run")); Assert.That(vm.Status, Is.EqualTo("Validation passed.")); });
    }
    private sealed class Handler : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }
        public string? LastPath { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method; LastPath = request.RequestUri!.AbsolutePath;
            var json = request.Method == HttpMethod.Get ? "[{\"id\":\"flow\",\"workspaceId\":\"ws\",\"application\":\"shop\",\"name\":\"Checkout works\",\"description\":\"Visible outcome\",\"initialPath\":\"/\",\"steps\":[],\"version\":1}]" : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
