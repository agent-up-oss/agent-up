using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.Applications.DTOs;
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

    private const string NavigationTokenScript = "(function(){return window.__nav || 'none';})()";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    // Showing the window attaches the platform WebView through a native control host on the UI
    // thread. A backend that never finishes initializing never returns, and the dispatcher call
    // below has nothing of its own to time out on, so bound it here rather than hang the run.
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan NavigationAttemptTimeout = TimeSpan.FromSeconds(8);
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
        var window = await ShowWindowAsync(() => CreateWindowAsync(
            new Uri("http://localhost:5000"),
            webViews,
            selectHttpPort: true,
            new DesktopServerStub(WorkspaceId, applicationPort)));

        return new DesktopBrowserHarness(window, webViews);
    }

    internal static async Task<DesktopBrowserHarness> LaunchAgainstServerAsync(
        Uri serverUrl,
        string? desktopApplication = null)
    {
        var webViews = new List<NativeWebView>();
        var window = await ShowWindowAsync(() => CreateWindowAsync(serverUrl, webViews, selectHttpPort: false));

        var harness = new DesktopBrowserHarness(window, webViews);
        if (desktopApplication is not null)
            await harness.SelectDesktopApplicationAsync(desktopApplication);
        await harness.WaitForWorkspaceWebViewAsync();
        return harness;
    }

    // A local async function so this is a real Task whatever InvokeAsync hands back, and so the
    // deadline applies to the whole UI-thread operation rather than to any one await inside it.
    private static async Task<MainWindow> ShowWindowAsync(Func<Task<MainWindow>> create)
    {
        async Task<MainWindow> OnUiThread() => await Dispatcher.UIThread.InvokeAsync(create);

        TestContext.Progress.WriteLine("Desktop harness: asking the UI thread to show MainWindow.");
        var launch = OnUiThread();
        if (await Task.WhenAny(launch, Task.Delay(LaunchTimeout)) != launch)
            throw new TimeoutException(
                $"Desktop never finished showing its MainWindow within {LaunchTimeout.TotalSeconds:0} seconds. "
                + "The platform WebView attaches through a native control host on the Avalonia UI thread, so a "
                + "WebView backend that never finishes initializing stops the run here with no test having started.");

        TestContext.Progress.WriteLine("Desktop harness: MainWindow shown.");
        return await launch;
    }

    private static async Task<MainWindow> CreateWindowAsync(
        Uri serverUrl,
        List<NativeWebView> webViews,
        bool selectHttpPort,
        HttpMessageHandler? handler = null)
    {
        var http = handler is null
            ? new HttpClient { BaseAddress = serverUrl }
            : new HttpClient(handler) { BaseAddress = serverUrl };
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
        TestContext.Progress.WriteLine("Desktop harness: MainWindow.Show() -- attaching the platform WebView.");
        mainWindow.Show();
        TestContext.Progress.WriteLine("Desktop harness: MainWindow.Show() returned; initializing the view model.");
        await viewModel.InitializeAsync();
        TestContext.Progress.WriteLine("Desktop harness: view model initialized.");
        if (selectHttpPort)
            SelectHttpPortTab(viewModel);
        return mainWindow;
    }

    internal async Task WaitForWorkspaceWebViewAsync()
    {
        var deadline = DateTimeOffset.UtcNow + DefaultTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_webViews.Count > 0)
                return;

            await Task.Delay(PollInterval);
        }

        Assert.Fail(
            "Desktop never created a NativeWebView for the workspace tab. "
            + await DescribeApplicationStateAsync());
    }

    internal async Task SelectDesktopApplicationAsync(string applicationName)
    {
        var deadline = DateTimeOffset.UtcNow + DefaultTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var selected = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var viewModel = (MainViewModel)Window.DataContext!;
                var application = viewModel.Applications.Applications
                    .FirstOrDefault(item => string.Equals(item.Name, applicationName, StringComparison.Ordinal));
                if (application is null)
                    return false;

                viewModel.SelectedApplicationTab = application;
                return viewModel.ShowDesktopView;
            });
            if (selected)
                return;

            await Task.Delay(PollInterval);
        }

        Assert.Fail($"Desktop never selected desktop application '{applicationName}'. {await DescribeApplicationStateAsync()}");
    }

    private async Task<string> DescribeApplicationStateAsync()
        => await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var viewModel = (MainViewModel)Window.DataContext!;
            var names = string.Join(", ", viewModel.Applications.Applications.Select(item => item.Name));
            return $"Shell={viewModel.SelectedShellTab}, ShowDesktopView={viewModel.ShowDesktopView}, "
                + $"SelectedSubTab={viewModel.SelectedSubTab?.GetType().Name ?? "(null)"}, "
                + $"Applications=[{names}]";
        });

    // Runs script inside the live workspace page and returns its result as the Desktop
    // browser controller sees it.
    internal Task<string?> EvalAsync(string script) => Window.EvalAsync(WorkspaceId, script);

    // Polls the live page until script produces expected. The WebView loads, injects Desktop's
    // page scripts, and completes navigations asynchronously in the platform engine, so tests
    // wait on observable page state instead of fixed delays.
    internal async Task WaitForScriptAsync(string script, string expected, string because, TimeSpan? timeout = null)
    {
        var attempt = await TryWaitForScriptAsync(script, expected, timeout ?? DefaultTimeout);
        if (attempt.Matched)
            return;

        var detail = attempt.Error is null ? string.Empty : $" Last evaluation error: {attempt.Error}.";
        Assert.Fail($"{because}. Expected '{expected}' from '{script}' but the last result was '{attempt.Last ?? "(null)"}'.{detail}");
    }

    private async Task<(bool Matched, string? Last, string? Error)> TryWaitForScriptAsync(
        string script,
        string expected,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        string? last = null;
        string? evaluationError = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                last = await EvalAsync(script);
                evaluationError = null;
                if (string.Equals(last, expected, StringComparison.Ordinal))
                    return (true, last, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Platform WebView engines refuse script evaluation while a navigation is in
                // flight, which is ordinary in the middle of an OAuth redirect chain. Polling
                // continues; the deadline is the real failure signal, and the last engine error
                // is reported with it.
                evaluationError = ex.Message;
            }

            await Task.Delay(PollInterval);
        }

        return (false, last, evaluationError);
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
    //
    // Desktop re-navigates a workspace tab on its own whenever the tab's port probe or health
    // state settles, which can land after — and supersede — a navigation a test just asked for.
    // That is correct product behaviour, so the harness simply re-issues its navigation until
    // the document it asked for is the one running.
    internal async Task<string> NavigateAsync(string url)
    {
        var token = Guid.NewGuid().ToString("N");
        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var target = $"{url}{separator}nav={token}";
        var deadline = DateTimeOffset.UtcNow + DefaultTimeout;
        (bool Matched, string? Last, string? Error) attempt = default;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Window.NavigateTo(WorkspaceId, target));
            attempt = await TryWaitForScriptAsync(NavigationTokenScript, token, NavigationAttemptTimeout);
            if (attempt.Matched)
                return token;
        }

        Assert.Fail(
            $"The Desktop WebView never loaded the page for navigation '{token}'. "
            + $"The last window.__nav was '{attempt.Last ?? "(null)"}'.");
        return token;
    }

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
        if (portTab is null)
            return;

        viewModel.SelectedSubTab = portTab;

        // Stand in for the port health a running Server reports over its event stream. Without
        // it the tab stays in the probing state, and Desktop keeps re-probing and re-navigating
        // the tab every few seconds — which is correct while a port's health is unknown, but is
        // not the state these tests are about.
        portTab.SetLedState(PortLedState.Healthy);
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
