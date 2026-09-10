using AgentUp.Desktop.Features.Validation.Providers;
namespace AgentUp.Desktop.Tests.Features.Validation.Provider;
public sealed class ValidationFlowApiClientTests
{
    [Test]
    public void ExportUri_scopes_path_to_workspace_and_flow()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var client = new ValidationFlowApiClient(http);
        Assert.That(client.ExportUri("workspace one", "flow/two").AbsoluteUri, Is.EqualTo("http://server/api/workspaces/workspace%20one/validation-flows/flow%2Ftwo/playwright"));
    }
}
