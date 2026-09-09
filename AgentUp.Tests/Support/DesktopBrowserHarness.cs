using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.Audit.Providers;
using AgentUp.Desktop.Features.Console.Providers;
using AgentUp.Desktop.Features.Database.Providers;
using AgentUp.Desktop.Features.Metrics.Providers;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AgentUp.Tests.Support;

// Launches the real Desktop MainWindow against the native display/WebView backend provided by
// the platform fixture adapter, points its browser tab at a local application server, and
// exposes script evaluation inside the live page so tests can assert on real DOM state.
internal sealed class DesktopBrowserHarness : IAsyncDisposable
{
    internal const string WorkspaceId = "ws-e2e";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly List<NativeWebView> _webViews;
    private MainWindow? _window;

    private DesktopBrowserHarness(MainWindow window, List<NativeWebView> webViews)
    {
        _window = window;
        _webViews = webViews;
    }

    internal MainWindow Window => _window
        ?? throw new InvalidOperationException("The Desktop browser harness has already been disposed.");

    // The WebViews Desktop created for workspace tabs, in creation order. Tests need the live
    // instance to raise the engine callbacks the platform engine raises, such as the new-window
    // request behind a sign-in popup.
    internal NativeWebView WorkspaceWebView => _webViews.Count > 0
        ? _webViews[0]
        : throw new InvalidOperationException("Desktop never created a workspace WebView.");

    internal static async Task<DesktopBrowserHarness> LaunchAsync(int applicationPort)
    {
        var webViews = new List<NativeWebView>();
        var window = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var handler = new DesktopServerStub(WorkspaceId, applicationPort);
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var workspaces = new WorkspaceApiClient(http);
            var console = new ConsoleApiClient(http);
            var metrics = new MetricsApiClient(http);
            var database = new DatabaseApiClient(http);
            var audit = new ApplicationAuditApiClient(http);
            var viewModel = MainViewModelFactory.Create(workspaces, console, metrics, database, audit);
            var mainWindow = new MainWindow(http) { DataContext = viewModel };
            mainWindow.WebViewFactory = () =>
            {
                var webView = new NativeWebView();
                webViews.Add(webView);
                return webView;
            };
            mainWindow.Show();

            // Loading the workspace list selects the application and its HTTP port sub-tab,
            // which is what drives the Desktop browser to navigate — the same path a user takes.
            await viewModel.InitializeAsync();
            SelectHttpPortTab(viewModel);
            return mainWindow;
        });

        return new DesktopBrowserHarness(window, webViews);
    }

    // Runs script inside the live workspace page and returns its result as the Desktop
    // browser controller sees it.
    internal Task<string?> EvalAsync(string script) => Window.EvalAsync(WorkspaceId, script);

    // Polls the live page until script produces expected. The WebView loads, injects Desktop's
    // page scripts, and completes navigations asynchronously in the platform engine, so tests
    // wait on observable page state instead of fixed delays.
    internal async Task WaitForScriptAsync(string script, string expected, string because, TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultTimeout);
        string? last = null;
        string? evaluationError = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                last = await EvalAsync(script);
                evaluationError = null;
                if (string.Equals(last, expected, StringComparison.Ordinal))
                    return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Platform WebView engines refuse script evaluation while a navigation is in
                // flight, which is ordinary in the middle of an OAuth redirect chain. Polling
                // continues; the deadline below is the real failure signal, and the last engine
                // error is reported with it.
                evaluationError = ex.Message;
            }

            await Task.Delay(PollInterval);
        }

        var detail = evaluationError is null ? string.Empty : $" Last evaluation error: {evaluationError}.";
        Assert.Fail($"{because}. Expected '{expected}' from '{script}' but the last result was '{last ?? "(null)"}'.{detail}");
    }

    // Runs a script whose whole point is to navigate away. The engine can tear the evaluation
    // context down before returning a result, so the script's observable outcome — the page the
    // navigation lands on — is what callers assert, not this call.
    internal async Task RunNavigatingScriptAsync(string script)
    {
        try
        {
            await EvalAsync(script);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TestContext.Progress.WriteLine($"Navigating script returned no result: {ex.Message}");
        }
    }

    // Navigates the workspace browser tab the way Desktop's own browser controller does and
    // waits until that exact document is live in the WebView.
    internal async Task<string> NavigateAsync(string url)
    {
        var token = Guid.NewGuid().ToString("N");
        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var target = $"{url}{separator}nav={token}";
        await Dispatcher.UIThread.InvokeAsync(() => Window.NavigateTo(WorkspaceId, target));
        await WaitForNavigationAsync(token);
        return token;
    }

    // Waits for the exact document the test just requested. Every test page echoes the nav
    // token from its URL, so a stale document left over from an earlier navigation can never
    // satisfy the wait.
    internal Task WaitForNavigationAsync(string navigationToken)
        => WaitForScriptAsync(
            "(function(){return window.__nav || 'none';})()",
            navigationToken,
            $"The Desktop WebView never loaded the page for navigation '{navigationToken}'");

    // Desktop injects its page scripts from NavigationCompleted, so the bridge becomes
    // available shortly after the document itself does.
    internal Task WaitForFilePickerBridgeAsync()
        => WaitForScriptAsync(
            "(window.__agentUpFilePickerInstalled === true && typeof window.invokeCSharpAction === 'function') ? 'ready' : 'pending'",
            "ready",
            "Desktop never installed the WebView upload bridge in the live page");

    private static void SelectHttpPortTab(MainViewModel viewModel)
    {
        var portTab = viewModel.SubTabs.OfType<PortSubTabViewModel>().FirstOrDefault(tab => tab.IsHttp);
        if (portTab is not null)
            viewModel.SelectedSubTab = portTab;
    }

    public async ValueTask DisposeAsync()
    {
        var window = _window;
        _window = null;
        if (window is null)
            return;

        await Dispatcher.UIThread.InvokeAsync(() => window.Close());
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }
}
