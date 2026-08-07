using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using AgentUp.Server;
using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Tests.Features.Browser.Headless;

// Exercises the full headless browser stack end-to-end: Chromium is downloaded, launched,
// navigated, screenshotted, and streamed over the RDP display endpoint.
//
// Chromium is cached in a stable temp directory so the download only happens once.
// First run may take ~2 minutes; subsequent runs are fast.
//
// Run: dotnet test AgentUp.Tests/ --filter "Category=HeadlessE2E"
[TestFixture, Category("HeadlessE2E")]
public sealed class HeadlessE2ETests
{
    private static readonly string DataDir =
        Path.Join(Path.GetTempPath(), "agentup-headless-e2e");

    private const string WorkspaceId = "00000000-0000-0000-0000-e2e000000001";

    private WebApplicationFactory<Program> _factory = null!;
    private BrowserSessionStore _store = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host =>
            {
                host.UseSetting("Storage:DataDirectory", DataDir);
                var chromium = FindSystemChromium();
                if (chromium is not null)
                    host.UseSetting("Browser:ExecutablePath", chromium);
            });

        _store = _factory.Services.GetRequiredService<BrowserSessionStore>();

        // Establish a session for all tests: this triggers Chromium download + launch.
        var nav = Navigate("about:blank", TimeoutMs: 60000);
        var result = await _store.DispatchAsync(nav, TimeSpan.FromMinutes(3), CancellationToken.None);

        Assert.That(result.Success, Is.True,
            $"Initial navigation failed — cannot run E2E suite. Error: {result.Error}");
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _factory.DisposeAsync();
    }

    [Test, CancelAfter(30000)]
    public async Task Navigate_returns_success(CancellationToken ct)
    {
        var result = await _store.DispatchAsync(
            Navigate("about:blank"), TimeSpan.FromSeconds(20), ct);

        Assert.That(result.Success, Is.True, result.Error);
    }

    [Test, CancelAfter(30000)]
    public async Task Screenshot_returns_valid_png_metadata(CancellationToken ct)
    {
        var result = await _store.DispatchAsync(
            Command(BrowserCommandKind.Screenshot), TimeSpan.FromSeconds(20), ct);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Data, Is.Not.Null);

        var screenshot = JsonSerializer.Deserialize<BrowserScreenshotResultDto>(
            result.Data!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.That(screenshot, Is.Not.Null);
        var bytes = Convert.FromBase64String(screenshot!.ImageBase64);
        Assert.Multiple(() =>
        {
            Assert.That(bytes.Length, Is.GreaterThan(8), "PNG file must have content");
            Assert.That(screenshot.MimeType, Is.EqualTo("image/png"));
            Assert.That(bytes[0], Is.EqualTo(0x89), "PNG signature byte 0");
            Assert.That(bytes[1], Is.EqualTo(0x50), "PNG signature byte 1 ('P')");
            Assert.That(bytes[2], Is.EqualTo(0x4E), "PNG signature byte 2 ('N')");
            Assert.That(bytes[3], Is.EqualTo(0x47), "PNG signature byte 3 ('G')");
        });
    }

    [Test, CancelAfter(30000)]
    public async Task Mode_endpoint_returns_headless(CancellationToken ct)
    {
        using var http = _factory.CreateClient();
        var body = await http.GetStringAsync("/api/browser/mode", ct);
        Assert.That(body, Is.EqualTo("headless"));
    }

    [Test, CancelAfter(30000)]
    public async Task Rdp_endpoint_streams_jpeg_frames(CancellationToken ct)
    {
        using var ws = await ConnectRdpAsync(WorkspaceId, ct);

        var frame = await ReceiveFrameAsync(ws, ct);

        Assert.Multiple(() =>
        {
            Assert.That(frame.Length, Is.GreaterThan(2), "JPEG frame must have content");
            Assert.That(frame[0], Is.EqualTo(0xFF), "JPEG SOI byte 0");
            Assert.That(frame[1], Is.EqualTo(0xD8), "JPEG SOI byte 1");
        });
    }

    [Test, CancelAfter(30000)]
    public async Task Two_rdp_clients_both_receive_frames(CancellationToken ct)
    {
        using var ws1 = await ConnectRdpAsync(WorkspaceId, ct);
        using var ws2 = await ConnectRdpAsync(WorkspaceId, ct);

        var r1Task = ReceiveFrameAsync(ws1, ct);
        var r2Task = ReceiveFrameAsync(ws2, ct);
        var results = await Task.WhenAll(r1Task, r2Task);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].Length, Is.GreaterThan(2), "First client frame must have content");
            Assert.That(results[1].Length, Is.GreaterThan(2), "Second client frame must have content");
        });
    }

    [Test, CancelAfter(30000)]
    public async Task Session_persists_after_client_reconnect(CancellationToken ct)
    {
        using (var ws = await ConnectRdpAsync(WorkspaceId, ct))
        {
            await ReceiveFrameAsync(ws, ct);
            ws.Abort();
        }

        // Small delay to let the server process the close.
        await Task.Delay(200, ct);

        // Reconnect — the Chromium session must still be alive.
        using var ws2 = await ConnectRdpAsync(WorkspaceId, ct);
        var frame = await ReceiveFrameAsync(ws2, ct);

        Assert.That(frame.Length, Is.GreaterThan(2),
            "Session should still be alive after client reconnect");
    }

    [Test, CancelAfter(30000)]
    public async Task CurrentUrl_endpoint_returns_url_when_session_active(CancellationToken ct)
    {
        using var http = _factory.CreateClient();
        var response = await http.GetAsync($"/api/browser/current-url/{WorkspaceId}", ct);

        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.That(body, Is.Not.Empty, "Current URL must be non-empty when session is active");
    }

    [Test, CancelAfter(30000)]
    public async Task Reload_endpoint_returns_no_content_when_session_active(CancellationToken ct)
    {
        using var http = _factory.CreateClient();
        var response = await http.PostAsync($"/api/browser/reload/{WorkspaceId}", null, ct);

        Assert.That((int)response.StatusCode, Is.EqualTo(204));
    }

    [Test, CancelAfter(30000)]
    public async Task Navigate_http_endpoint_returns_no_content_when_headless_active(CancellationToken ct)
    {
        using var http = _factory.CreateClient();
        var response = await http.PostAsync(
            $"/api/browser/navigate/{WorkspaceId}?url=http%3A%2F%2Flocalhost%3A1",
            null, ct);

        // 204 (navigated) or 400 (page load error) — both mean the endpoint dispatched successfully.
        Assert.That((int)response.StatusCode, Is.AnyOf(204, 400),
            "Navigate endpoint must dispatch when headless is configured");
    }

    private async Task<WebSocket> ConnectRdpAsync(string workspaceId, CancellationToken ct)
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        var uri = new Uri($"ws://localhost/api/browser/rdp/{workspaceId}");
        return await wsClient.ConnectAsync(uri, ct);
    }

    private static async Task<byte[]> ReceiveFrameAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[256 * 1024];
        var result = await ws.ReceiveAsync(buffer, ct);
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Binary),
            "RDP display frames must be binary WebSocket messages");
        return buffer[..result.Count];
    }

    private static BrowserCommandDto Navigate(string url, int TimeoutMs = 10000) =>
        new(Guid.NewGuid(), WorkspaceId, BrowserCommandKind.Navigate, url, null, null, null, TimeoutMs);

    private static BrowserCommandDto Command(BrowserCommandKind kind) =>
        new(Guid.NewGuid(), WorkspaceId, kind, null, null, null, null, 10000);

    private static string? FindSystemChromium()
    {
        var env = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;

        foreach (var name in new[] { "chromium", "chromium-browser", "google-chrome", "chrome" })
        {
            try
            {
                // Use ArgumentList (not Arguments string) so .NET passes the args verbatim
                // without shell-quote processing, which is inconsistent across platforms.
                var psi = new ProcessStartInfo("which", name)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var proc = Process.Start(psi);
                var path = proc?.StandardOutput.ReadToEnd().Trim();
                proc?.WaitForExit();
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException
                                            or System.ComponentModel.Win32Exception)
            {
                continue;
            }
        }
        return null;
    }
}
