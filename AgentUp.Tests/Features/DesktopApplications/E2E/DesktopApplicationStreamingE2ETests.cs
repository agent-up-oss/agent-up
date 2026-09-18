using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Tests.Support;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AgentUp.Tests.Features.DesktopApplications.E2E;

[TestFixture, Category("E2E")]
[Platform(Include = "Linux")]
[Timeout(180000), CancelAfter(180000)]
public sealed class DesktopApplicationStreamingE2ETests
{
    private const string ApplicationName = "Sample Desktop";
    private const string FrameScript =
        "(function(){var s=document.getElementById('status');var c=document.getElementById('display');" +
        "if(!s||!c)return 'missing';return s.hidden?'framed':String(s.textContent||'pending');})()";

    private static DesktopStreamingServer? Server;
    private static string? WorkspaceId;
    private static DesktopBrowserHarness? Desktop;
    private static Exception? StartFailure;

    [SetUp]
    public async Task EnsureStreamedDesktop()
    {
        if (Desktop is not null)
            return;
        if (StartFailure is not null)
            throw StartFailure;

        try
        {
            await StartStreamedDesktopAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not IgnoreException)
        {
            StartFailure = ex;
            throw;
        }
    }

    private static async Task StartStreamedDesktopAsync()
    {

        var example = FindLinuxDesktopExample();
        if (example is null)
            Assert.Ignore("Examples/linux-desktop was not found next to agent-up.sln.");

        Server = await DesktopStreamingServer.StartAsync();
        TestContext.Progress.WriteLine($"In-process Server listening at {Server.BaseUri}.");

        using var created = await Server.Client.PostAsJsonAsync(
            "/api/workspaces",
            ProductDomain.Workspace()
                .Named("desktop-e2e")
                .At(example)
                .WithDesktopApplication(new DesktopApplicationDefinition(
                    ApplicationName,
                    "dotnet run --project LinuxDesktop.csproj --no-launch-profile",
                    ".",
                    new DesktopWindowDefinition(800, 600),
                    Install: "dotnet build LinuxDesktop.csproj --nologo --no-incremental"))
                .Build());
        if (!created.IsSuccessStatusCode)
            Assert.Fail($"Register workspace failed ({(int)created.StatusCode}): {await created.Content.ReadAsStringAsync()}");
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        WorkspaceId = body.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("The Server did not return a workspace id.");

        using var start = await Server.Client.PostAsync($"/api/workspaces/{WorkspaceId}/start", null);
        if (!start.IsSuccessStatusCode)
            Assert.Fail($"Desktop streaming start failed: {await start.Content.ReadAsStringAsync()}");
        TestContext.Progress.WriteLine("Workspace start completed; launching Desktop against the Server.");

        Desktop = await DesktopBrowserHarness.LaunchAgainstServerAsync(Server.BaseUri, ApplicationName);
        await WaitForViewerFrameAsync(Desktop.WorkspaceWebView, "Desktop never rendered a streamed desktop frame.");
    }

    [OneTimeTearDown]
    public async Task StopStreamedDesktop()
    {
        if (Desktop is not null)
            await Desktop.DisposeAsync();
        if (Server is not null)
        {
            try
            {
                if (WorkspaceId is not null)
                    await Server.Client.PostAsync($"/api/workspaces/{WorkspaceId}/stop", null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                TestContext.Progress.WriteLine(ex.Message);
            }

            await Server.DisposeAsync();
        }
    }

    [Test]
    public async Task Desktop_client_renders_the_streamed_x11_session()
    {
        var last = await Dispatcher.UIThread.InvokeAsync(() => Desktop!.WorkspaceWebView.InvokeScript(FrameScript));
        Assert.That(last, Does.Contain("framed"));
    }

    [Test]
    public async Task Mobile_style_viewer_ticket_renders_the_same_session()
    {
        using var ticketResponse = await Server!.Client.PostAsync(
            $"/api/desktop-applications/{Uri.EscapeDataString(WorkspaceId!)}/{Uri.EscapeDataString(ApplicationName)}/viewer-ticket",
            null);
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<DesktopViewerTicketResponse>();
        Assert.That(ticket?.ViewerUrl, Is.Not.Null.And.Not.Empty);

        var viewerUri = new Uri(Server.BaseUri, ticket!.ViewerUrl);
        NativeWebView? viewer = null;
        Window? window = null;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            viewer = new NativeWebView();
            window = new Window
            {
                Title = "mobile-viewer",
                Width = 800,
                Height = 600,
                Content = viewer
            };
            window.Show();
            viewer.Source = viewerUri;
        });

        try
        {
            await WaitForViewerFrameAsync(viewer!, "A second viewer ticket never received a desktop frame.");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window?.Close());
        }
    }

    private static async Task WaitForViewerFrameAsync(NativeWebView webView, string because)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(45);
        string? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                last = await Dispatcher.UIThread.InvokeAsync(() => webView.InvokeScript(FrameScript));
                if (string.Equals(last, "framed", StringComparison.Ordinal)
                    || (last is not null && last.Contains("framed", StringComparison.Ordinal)))
                    return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex.Message;
            }

            await Task.Delay(200);
        }

        Assert.Fail($"{because} Last viewer state was '{last ?? "(null)"}'.");
    }

    private static string? FindLinuxDesktopExample()
    {
        var directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var example = Path.Join(directory, "Examples", "linux-desktop", "LinuxDesktop.csproj");
            if (File.Exists(example))
                return Path.GetDirectoryName(example);

            var parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
                break;

            directory = parent;
        }

        return null;
    }
}
