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

    [Test]
    public void ExportUri_escapes_fragment_and_query_characters_as_path_data()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://server/") };
        var client = new ValidationFlowApiClient(http);

        Assert.That(client.ExportUri("workspace?one", "flow#two").AbsoluteUri,
            Is.EqualTo("http://server/api/workspaces/workspace%3Fone/validation-flows/flow%23two/playwright"));
    }
}
