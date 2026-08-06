using AgentUp.Server;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Tests.Features.Browser.Headless;

// Verifies HTTP endpoint shapes for the headless browser slice without launching Chromium.
// Chromium download is lazy so WebApplicationFactory starts without triggering it.
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
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Mode_endpoint_returns_headless()
    {
        var response = await _client.GetAsync("/api/browser/mode");

        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Is.EqualTo("headless"));
    }

    [Test]
    public async Task Viewer_endpoint_returns_html_with_canvas_and_workspaceId()
    {
        var response = await _client.GetAsync("/api/browser/rdp-viewer?workspaceId=test-ws");

        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("<canvas"));
            Assert.That(body, Does.Contain("test-ws"));
            Assert.That(body, Does.Contain("/api/browser/rdp/"));
            Assert.That(body, Does.Contain("id=\"ai-badge\""));
            Assert.That(body, Does.Contain("connectStream()"));
            Assert.That(body, Does.Contain("setTimeout"));
        });
    }

    [Test]
    public async Task Remote_session_endpoint_returns_ironrdp_viewer_metadata()
    {
        var response = await _client.GetAsync("/api/browser/remote-session/test-ws");

        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("\"workspaceId\":\"test-ws\""));
            Assert.That(body, Does.Contain("\"transport\":\"rdp\""));
            Assert.That(body, Does.Contain("\"displayWebSocketPath\":\"/api/browser/rdp/test-ws\""));
            Assert.That(body, Does.Contain("\"latestFramePath\":\"/api/browser/rdp/test-ws/frame\""));
            Assert.That(body, Does.Contain("\"selectedPresetId\":\"desktop\""));
            Assert.That(body, Does.Contain("\"touchCapable\":true"));
        });
    }

    [Test]
    public async Task Rdp_endpoint_returns_400_for_plain_http_request()
    {
        var response = await _client.GetAsync("/api/browser/rdp/ws-1");
        Assert.That((int)response.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task CurrentUrl_endpoint_returns_404_when_no_session()
    {
        var response = await _client.GetAsync("/api/browser/current-url/ws-1");
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
