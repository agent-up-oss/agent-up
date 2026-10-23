using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Providers;
namespace AgentUp.Desktop.Tests.Features.Validation.Provider;
public sealed class ValidationFlowApiClientTests
{
    [Test]
    public async Task Get_defaultsAMissingSurface_toApplication()
    {
        using var http = new HttpClient(new SurfaceHandler("""{"id":"flow","workspaceId":"ws","application":"shop","name":"Checkout","description":"Cart","initialPath":"/","initialExpectations":[],"steps":[],"version":1}"""))
        {
            BaseAddress = new Uri("http://server/")
        };
        var client = new ValidationFlowApiClient(http);

        var flow = await client.GetAsync("ws", "flow");

        Assert.That(flow!.Surface, Is.EqualTo(ValidationSurfaceDto.Application));
    }

    [Test]
    public async Task Get_readsDesktopSurface()
    {
        using var http = new HttpClient(new SurfaceHandler("""{"id":"flow","workspaceId":"ws","application":"Desktop","name":"Open Git","description":"Git","initialPath":"workspace/git","initialExpectations":[],"steps":[],"version":1,"surface":"Desktop"}"""))
        {
            BaseAddress = new Uri("http://server/")
        };
        var client = new ValidationFlowApiClient(http);

        var flow = await client.GetAsync("ws", "flow");

        Assert.Multiple(() =>
        {
            Assert.That(flow!.Surface, Is.EqualTo(ValidationSurfaceDto.Desktop));
            Assert.That(flow.InitialPath, Is.EqualTo("workspace/git"));
        });
    }

    private sealed class SurfaceHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    [Test]
    public void ExportUri_scopes_path_to_workspace_and_flow()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var client = new ValidationFlowApiClient(http);
        Assert.That(client.ExportUri("workspace one", "flow/two").AbsoluteUri, Is.EqualTo("http://server/api/workspaces/workspace%20one/validation-flows/flow%2Ftwo/playwright"));
    }

    [Test]
    public void ExportUri_escapes_fragment_and_query_characters_as_path_data()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var client = new ValidationFlowApiClient(http);

        Assert.That(client.ExportUri("workspace?one", "flow#two").AbsoluteUri,
            Is.EqualTo("http://server/api/workspaces/workspace%3Fone/validation-flows/flow%23two/playwright"));
    }
}
