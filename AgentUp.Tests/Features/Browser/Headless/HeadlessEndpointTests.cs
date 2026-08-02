using AgentUp.Server;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Tests.Features.Browser.Headless;

// Verifies HTTP endpoint shapes for the headless browser slice without launching Chromium.
// Both viewer and mode endpoints are registered unconditionally, so polling mode is sufficient.
//
// Run: dotnet test AgentUp.Tests/ --filter "Category=Headless"
[TestFixture, Category("Headless")]
public sealed class HeadlessEndpointTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host => host.UseSetting("Browser:Mode", "polling"));
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Mode_endpoint_returns_polling_when_configured_as_polling()
    {
        var response = await _client.GetAsync("/api/browser/mode");

        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Is.EqualTo("polling"));
    }

    [Test]
    public async Task Viewer_endpoint_returns_html_with_canvas_and_workspaceId()
    {
        var response = await _client.GetAsync("/api/browser/viewer?workspaceId=test-ws");

        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("<canvas"));
            Assert.That(body, Does.Contain("test-ws"));
        });
    }

    [Test]
    public async Task Screencast_endpoint_returns_400_for_plain_http_request()
    {
        var response = await _client.GetAsync("/api/browser/screencast/ws-1");
        Assert.That((int)response.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task CurrentUrl_endpoint_returns_404_when_no_session()
    {
        var response = await _client.GetAsync("/api/browser/current-url/ws-1");
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task Navigate_endpoint_returns_404_when_headless_not_configured()
    {
        var response = await _client.PostAsync("/api/browser/navigate/ws-1?url=http%3A%2F%2Flocalhost%3A3000", null);
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task NavigateBack_endpoint_returns_404_when_no_session()
    {
        var response = await _client.PostAsync("/api/browser/navigate-back/ws-1", null);
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task NavigateForward_endpoint_returns_404_when_no_session()
    {
        var response = await _client.PostAsync("/api/browser/navigate-forward/ws-1", null);
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task Reload_endpoint_returns_404_when_no_session()
    {
        var response = await _client.PostAsync("/api/browser/reload/ws-1", null);
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
    }
}
