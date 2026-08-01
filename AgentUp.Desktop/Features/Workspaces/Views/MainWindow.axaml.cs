using System.Diagnostics;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Browser.Interfaces;
using AgentUp.Desktop.Features.Browser.Providers;
using AgentUp.Desktop.Features.Browser.Services;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Repositories;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>, IBrowserWindowHost
{
    // One NativeWebView per (workspaceId, port) pair — keyed by tabKey = "workspaceId:port".
    // Switching between port tabs only toggles IsVisible; the WebView is never navigated away,
    // preserving full page state (scroll position, open accordions, JS memory, auth session).
    private readonly Dictionary<string, NativeWebView> _webViews = new();
    // Errors keyed by workspaceId (not tabKey) so the banner persists across tab switches.
    private readonly Dictionary<string, string> _webViewErrors = new();
    // Last successfully navigated http URL per tabKey; absent means tab is in error state.
    private readonly Dictionary<string, string> _lastKnownBrowserUrls = new();
    private readonly Dictionary<string, int> _navigationVersions = new();
    // workspaceId → tabKey of the tab the agent last navigated to (for EvalAsync routing).
    private readonly Dictionary<string, string> _agentActiveTabKeys = new();
    private readonly CompositeDisposable _subscriptions = new();
    private readonly DispatcherTimer _addressPollTimer;
    private readonly BrowserCommandPoller _browserPoller;
    private readonly HttpClient _serverHttp;
    private WorkspaceEventClient? _workspaceEventClient;
    private string? _activeWorkspaceId;
    private string? _activeTabKey;   // tabKey of the currently visible WebView
    private bool _isClosed;
    private NativeWebView? _consoleWebView;
    private Panel? _consoleOverlay;
    private bool _consoleSelecting;
    private const int ConsoleDefaultDisplayLines = 2_000;
    private static readonly HttpClient PortProbeHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    // Overrideable in tests to inject a factory that throws without needing GTK.
    internal Func<NativeWebView> WebViewFactory { get; set; } = () => new NativeWebView();
    // Overrideable in tests to bypass HTTP probing.
    internal Func<Uri, Task<string?>> BrowserProbe { get; set; } = ProbeBrowserDestinationAsync;
    internal bool HasBrowserResourcesForTests =>
        _addressPollTimer.IsEnabled
        || _webViews.Count > 0
        || _webViewErrors.Count > 0
        || _lastKnownBrowserUrls.Count > 0
        || _activeWorkspaceId is not null;

    private const string SelectionJs =
        "(function(){" +
        "if(!document.getElementById('_au_sel')){" +
        "var st=document.createElement('style');st.id='_au_sel';" +
        "st.textContent='::selection{background-color:#0f7a45!important;color:#f5fbf7!important}';" +
        "(document.head||document.documentElement).appendChild(st);}" +
        "var active=false;" +
        "window._selStart=function(x,y){" +
        "active=true;" +
        "var r=document.caretRangeFromPoint(x,y);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "s.removeAllRanges();" +
        "var g=document.createRange();" +
        "g.setStart(r.startContainer,r.startOffset);" +
        "g.collapse(true);" +
        "s.addRange(g);" +
        "};" +
        "window._selEnd=function(){active=false;};" +
        "document.addEventListener('mousemove',function(e){" +
        "if(!active)return;" +
        "var r=document.caretRangeFromPoint(e.clientX,e.clientY);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "if(!s.anchorNode)return;" +
        "try{s.extend(r.startContainer,r.startOffset);}catch(ex){}" +
        "},true);" +
        "})();";

    private const string ConsoleJs =
        "(function(){" +
        "function focus(){var c=document.getElementById('content');if(c)c.focus({preventScroll:true});}" +
        "window._selStart=function(x,y){" +
        "focus();" +
        "var r=document.caretRangeFromPoint(x,y);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "s.removeAllRanges();" +
        "var g=document.createRange();" +
        "g.setStart(r.startContainer,r.startOffset);" +
        "g.collapse(true);" +
        "s.addRange(g);" +
        "};" +
        "window._selExtend=function(x,y){" +
        "var r=document.caretRangeFromPoint(x,y);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "if(!s.anchorNode)return;" +
        "try{s.extend(r.startContainer,r.startOffset);}catch(ex){}" +
        "};" +
        "window._selWord=function(x,y){" +
        "focus();" +
        "var r=document.caretRangeFromPoint(x,y);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "s.removeAllRanges();s.addRange(r);" +
        "s.modify('expand','backward','word');" +
        "s.modify('extend','forward','word');" +
        "};" +
        "window._selLine=function(x,y){" +
        "focus();" +
        "var r=document.caretRangeFromPoint(x,y);" +
        "if(!r)return;" +
        "var s=window.getSelection();" +
        "s.removeAllRanges();s.addRange(r);" +
        "s.modify('expand','backward','lineboundary');" +
        "s.modify('extend','forward','lineboundary');" +
        "};" +
        "window._scroll=function(dy){" +
        "var c=document.getElementById('content');" +
        "if(c)c.scrollTop+=dy;" +
        "};" +
        "})();";

    public MainWindow()
    {
        InitializeComponent();
        SetWindowIcon();
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        _addressPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _addressPollTimer.Tick += OnAddressPollTimerTick;
        _addressPollTimer.Start();
        var serverUrl = Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL") ?? "http://localhost:5000";
        _serverHttp = new HttpClient { BaseAddress = new Uri(serverUrl) };
        _browserPoller = new BrowserCommandPoller(new BrowserCommandHttpClient(_serverHttp), this);
        _browserPoller.Start();
    }

    // IBrowserWindowHost — derive workspace IDs from the tab-keyed dictionary.
    IReadOnlyCollection<string> IBrowserWindowHost.ActiveWorkspaceIds =>
        _webViews.Keys.Select(WorkspaceFromTabKey).Distinct().ToList();

    Task<string?> IBrowserWindowHost.EvalAsync(string workspaceId, string script) =>
        EvalAsync(workspaceId, script);

    void IBrowserWindowHost.NavigateTo(string workspaceId, string? url) =>
        NavigateBackground(workspaceId, url);

    // Navigates a workspace's tab WebView from the agent side. Switches the visible application
    // tab when the target port belongs to a different app within the same active workspace.
    private void NavigateBackground(string workspaceId, string? url)
    {
        if (_isClosed || url is null) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        var tabKey = TabKey(workspaceId, uri);
        _agentActiveTabKeys[workspaceId] = tabKey;

        SwitchApplicationTabForUrl(workspaceId, url);

        if (!TryGetOrCreateWebView(tabKey, workspaceId, url, out var webView, out var destinationUrl)) return;

        // Avoid force-reloading a tab the agent is already on.
        if (_lastKnownBrowserUrls.TryGetValue(tabKey, out var currentUrl)
            && string.Equals(currentUrl, destinationUrl, StringComparison.Ordinal))
            return;

        var navigationVersion = _navigationVersions.GetValueOrDefault(tabKey) + 1;
        _navigationVersions[tabKey] = navigationVersion;
        _ = NavigatePortWebViewAsync(tabKey, workspaceId, webView, new Uri(destinationUrl), navigationVersion);
    }

    // Switches the application and sub-tab to match the port in url, so the user can watch
    // the agent's navigation. Only acts when url targets the currently active workspace.
    private void SwitchApplicationTabForUrl(string workspaceId, string url)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.Sidebar.SelectedWorkspace?.Id != workspaceId) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        var targetPort = uri.Port;
        var matchingApp = vm.Applications.Applications
            .FirstOrDefault(a => a.AllocatedPorts.Any(p => p.AllocatedPort == targetPort));
        if (matchingApp is null) return;

        vm.PreloadPortUrl(url);

        if (vm.Applications.SelectedApplication != matchingApp)
            vm.Applications.SelectedApplication = matchingApp;

        // For apps with multiple HTTP tabs, also select the exact port tab.
        var targetTab = vm.SubTabs.OfType<PortSubTabViewModel>().FirstOrDefault(t => t.AllocatedPort == targetPort);
        if (targetTab is not null && vm.SelectedSubTab != targetTab)
            vm.SelectedSubTab = targetTab;
    }

    private void SetWindowIcon()
    {
        try
        {
            var iconPath = FindWindowIconPath();
            if (iconPath is null) return;

            using var stream = File.OpenRead(iconPath);
            Icon = new WindowIcon(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }

    private static string? FindWindowIconPath()
    {
        var outputPath = Path.Join(AppContext.BaseDirectory, "media", "logo.png");
        if (File.Exists(outputPath)) return outputPath;

        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            var candidate = Path.Join(dir, "media", "logo.png");
            if (File.Exists(candidate)) return candidate;

            var parent = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (parent == dir) break;
            dir = parent;
        }

        return null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        _workspaceEventClient?.Stop();
        _workspaceEventClient = null;

        base.OnDataContextChanged(e);
        if (DataContext is not MainViewModel vm) return;

        var eventHttp = new HttpClient { BaseAddress = _serverHttp.BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
        _workspaceEventClient = new WorkspaceEventClient(eventHttp, vm.Sidebar);
        _workspaceEventClient.Start();

        _subscriptions.Clear();
        vm.BrowserNavigation.Subscribe(nav =>
            Dispatcher.UIThread.Post(() => HandleNavigation(nav.WorkspaceId, nav.Url)))
            .DisposeWith(_subscriptions);
        vm.BrowserCommands.Subscribe(command =>
            Dispatcher.UIThread.Post(() => HandleBrowserCommand(command)))
            .DisposeWith(_subscriptions);
        vm.Tutorial.WhenAnyValue(t => t.IsVisible)
            .DistinctUntilChanged()
            .Subscribe(isVisible =>
                Dispatcher.UIThread.Post(() => ApplyTutorialWebViewVisibility(isVisible)))
            .DisposeWith(_subscriptions);
        ApplyTutorialWebViewVisibility(vm.Tutorial.IsVisible);
        vm.Console.WhenAnyValue(c => c.IsLoading)
            .Skip(1)
            .Where(loading => !loading)
            .Where(_ => vm.ShowConsole)
            .Subscribe(_ => Dispatcher.UIThread.Post(RefreshConsoleWebView))
            .DisposeWith(_subscriptions);
        vm.WhenAnyValue(v => v.ShowConsole)
            .Where(visible => visible && !vm.Console.IsLoading)
            .Subscribe(_ => Dispatcher.UIThread.Post(RefreshConsoleWebView))
            .DisposeWith(_subscriptions);
        vm.Console.WhenAnyValue(c => c.ShowAllLines)
            .Where(all => all && vm.ShowConsole)
            .Subscribe(_ => Dispatcher.UIThread.Post(RefreshConsoleWebView))
            .DisposeWith(_subscriptions);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _workspaceEventClient?.Stop();
        _browserPoller.Stop();
        _addressPollTimer.Stop();
        _addressPollTimer.Tick -= OnAddressPollTimerTick;
        _subscriptions.Dispose();
        DestroyWorkspaceWebViews();
        DestroyConsoleWebView();
        base.OnClosed(e);
    }

    internal void NavigateTo(string workspaceId, string? url) => HandleNavigation(workspaceId, url);

    // Evaluates a script in the tab the agent last navigated to for the given workspace.
    internal async Task<string?> EvalAsync(string workspaceId, string script)
    {
        if (!_agentActiveTabKeys.TryGetValue(workspaceId, out var tabKey)) return null;
        if (!_webViews.TryGetValue(tabKey, out var webView)) return null;
        var result = await Dispatcher.UIThread.InvokeAsync(() => webView.InvokeScript(script));
        return NormalizeScriptResult(result);
    }

    private void UpdateErrorDisplay(string? workspaceId)
    {
        if (IsTutorialVisible())
        {
            WebViewErrorBanner.IsVisible = false;
            return;
        }

        if (workspaceId is not null && _webViewErrors.TryGetValue(workspaceId, out var error))
        {
            WebViewErrorText.Text = error;
            WebViewErrorBanner.IsVisible = true;
        }
        else
        {
            WebViewErrorBanner.IsVisible = false;
        }
    }

    private void HandleNavigation(string? workspaceId, string? url)
    {
        if (_isClosed) return;

        var tutorialVisible = IsTutorialVisible();

        // Derive the tab key from the URL's port. Non-HTTP URLs and null urls have no tab key.
        string? tabKey = null;
        if (workspaceId is not null && url is not null
            && Uri.TryCreate(url, UriKind.Absolute, out var navUri)
            && navUri.Scheme is "http" or "https")
            tabKey = TabKey(workspaceId, navUri);

        ActivateTab(workspaceId, tabKey, tutorialVisible);

        if (tabKey is null || workspaceId is null || url is null) return;

        // If a WebView already exists for this tab, its full page state is preserved.
        // Only re-navigate when the tab is in error state (absent from _lastKnownBrowserUrls)
        // so a port coming back online automatically recovers the view.
        if (_webViews.TryGetValue(tabKey, out var existingWebView))
        {
            existingWebView.IsVisible = !tutorialVisible;
            if (_lastKnownBrowserUrls.ContainsKey(tabKey)) return;

            // Error-state recovery: re-probe and re-navigate.
            var errNavVer = _navigationVersions.GetValueOrDefault(tabKey) + 1;
            _navigationVersions[tabKey] = errNavVer;
            _ = NavigatePortWebViewAsync(tabKey, workspaceId, existingWebView, new Uri(url), errNavVer);
            return;
        }

        // First visit to this tab: create WebView and navigate.
        if (!TryGetOrCreateWebView(tabKey, workspaceId, url, out var webView, out var destinationUrl)) return;

        webView.IsVisible = !tutorialVisible;
        var destination = new Uri(destinationUrl);
        var navigationVersion = _navigationVersions.GetValueOrDefault(tabKey) + 1;
        _navigationVersions[tabKey] = navigationVersion;
        _ = NavigatePortWebViewAsync(tabKey, workspaceId, webView, destination, navigationVersion);
    }

    private void ActivateTab(string? workspaceId, string? tabKey, bool tutorialVisible)
    {
        if (workspaceId == _activeWorkspaceId && tabKey == _activeTabKey) return;

        if (_activeTabKey is not null && _webViews.TryGetValue(_activeTabKey, out var previous))
            previous.IsVisible = false;

        _activeWorkspaceId = workspaceId;
        _activeTabKey = tabKey;

        if (!tutorialVisible && tabKey is not null && _webViews.TryGetValue(tabKey, out var next))
            next.IsVisible = true;

        UpdateErrorDisplay(workspaceId);
    }

    private bool TryGetOrCreateWebView(
        string tabKey,
        string workspaceId,
        string requestedUrl,
        out NativeWebView webView,
        out string destinationUrl)
    {
        destinationUrl = requestedUrl;
        if (_webViews.TryGetValue(tabKey, out webView!))
            return true;

        try
        {
            webView = CreateWorkspaceWebView(tabKey, workspaceId);
            _webViews[tabKey] = webView;
            _webViewErrors.Remove(workspaceId);

            // Restore the last URL visited on this port; fall back to the base URL.
            destinationUrl = BrowserUrlStore.Read(workspaceId, requestedUrl) ?? requestedUrl;

            // Start hidden; HandleNavigation / NavigateBackground makes it visible as needed.
            webView.IsVisible = false;
            PortPane.Children.Add(webView);
            UpdateErrorDisplay(workspaceId);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _webViewErrors[workspaceId] = $"Could not start the browser: {ex.Message}";
            UpdateErrorDisplay(workspaceId);
            return false;
        }
    }

    private NativeWebView CreateWorkspaceWebView(string tabKey, string workspaceId)
    {
        var webView = WebViewFactory();
        webView.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(NativeWebView.Source))
                UpdateAddressFromWebView(tabKey, workspaceId, webView.Source);
        };
        webView.EnvironmentRequested += (_, e) => ConfigureWebViewProfile(workspaceId, e);

        var firstNavDone = false;
        webView.NavigationCompleted += (_, e) =>
        {
            if (!e.IsSuccess)
            {
                if (e.Request is { } failedUri && failedUri.Scheme is "http" or "https")
                {
                    ShowBrowserErrorPage(
                        tabKey,
                        workspaceId,
                        webView,
                        failedUri,
                        "Could not load page",
                        "The embedded browser could not load this route.");
                }
                return;
            }

            if (e.Request is not { } uri) return;
            UpdateAddressFromWebView(tabKey, workspaceId, uri);
            _ = webView.InvokeScript(SelectionJs);
            if (firstNavDone) return;
            firstNavDone = true;
            ForceFirstWebKitPaint(tabKey, webView);
        };

        return webView;
    }

    private async Task NavigatePortWebViewAsync(
        string tabKey,
        string workspaceId,
        NativeWebView webView,
        Uri destination,
        int navigationVersion)
    {
        var errorHtml = await BrowserProbe(destination);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!CanTouchWebView(tabKey, webView)) return;
            if (_navigationVersions.GetValueOrDefault(tabKey) != navigationVersion) return;

            if (errorHtml is null)
            {
                NavigateWebView(webView, destination);
            }
            else
            {
                // Evict cached URL so the next tab-switch triggers a real navigation attempt
                // rather than assuming the browser is still at the previous http URL.
                _lastKnownBrowserUrls.Remove(tabKey);
                NavigateWebView(webView, WriteBrowserErrorPage(workspaceId, errorHtml));
            }
        });
    }

    private static async Task<string?> ProbeBrowserDestinationAsync(Uri destination)
    {
        if (destination.Scheme is not ("http" or "https") || !destination.IsLoopback)
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, destination);
            using var response = await PortProbeHttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            if ((int)response.StatusCode < 500)
                return null;

            return BuildBrowserErrorHtml(
                $"{response.ReasonPhrase ?? "Not found"} {(int)response.StatusCode}",
                response.ReasonPhrase ?? "Request failed",
                destination);
        }
        catch (HttpRequestException ex)
        {
            return BuildBrowserErrorHtml(
                "Could not reach app",
                ex.Message,
                destination);
        }
        catch (TaskCanceledException)
        {
            return BuildBrowserErrorHtml(
                "App did not respond",
                "The request timed out before the embedded browser loaded the page.",
                destination);
        }
    }

    private void ShowBrowserErrorPage(
        string tabKey,
        string workspaceId,
        NativeWebView webView,
        Uri destination,
        string title,
        string detail)
    {
        if (!CanTouchWebView(tabKey, webView)) return;
        _lastKnownBrowserUrls.Remove(tabKey);
        var html = BuildBrowserErrorHtml(title, detail, destination);
        NavigateWebView(webView, WriteBrowserErrorPage(workspaceId, html));
    }

    private static void NavigateWebView(NativeWebView webView, Uri destination)
    {
        if (string.Equals(webView.Source?.ToString(), destination.ToString(), StringComparison.Ordinal))
        {
            webView.Source = new Uri("about:blank");
            Dispatcher.UIThread.Post(() => webView.Source = destination, DispatcherPriority.Background);
            return;
        }

        webView.Source = destination;
    }

    private void ForceFirstWebKitPaint(string tabKey, NativeWebView webView)
    {
        // WebKit renders content into the native window but GTK only composites it when
        // the embedded window receives an Expose event. A hide/show at separate dispatcher
        // priorities forces the repaint after first navigation.
        Dispatcher.UIThread.Post(() =>
        {
            if (!CanTouchWebView(tabKey, webView) || !webView.IsVisible) return;
            webView.IsVisible = false;
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (CanTouchWebView(tabKey, webView))
                        webView.IsVisible = true;
                },
                DispatcherPriority.Background);
        });
    }

    private bool CanTouchWebView(string tabKey, NativeWebView webView)
        => !_isClosed
           && _webViews.TryGetValue(tabKey, out var current)
           && ReferenceEquals(current, webView);

    private bool IsTutorialVisible()
        => DataContext is MainViewModel { Tutorial.IsVisible: true };

    private void ApplyTutorialWebViewVisibility(bool tutorialVisible)
    {
        if (_isClosed) return;

        foreach (var webView in _webViews.Values)
            webView.IsVisible = false;

        if (tutorialVisible)
        {
            WebViewErrorBanner.IsVisible = false;
            return;
        }
        UpdateErrorDisplay(_activeWorkspaceId);
        if (_activeTabKey is null) return;
        if (!_webViews.TryGetValue(_activeTabKey, out var active)) return;
        if (DataContext is not MainViewModel { ShowPortView: true }) return;

        active.IsVisible = true;
    }

    private void HandleBrowserCommand(BrowserCommand command)
    {
        if (_isClosed) return;
        if (_activeTabKey is null || _activeWorkspaceId is null) return;
        if (!_webViews.TryGetValue(_activeTabKey, out var webView)) return;

        switch (command)
        {
            case BrowserCommand.Back:
                if (webView.CanGoBack)
                    webView.GoBack();
                break;
            case BrowserCommand.Forward:
                if (webView.CanGoForward)
                    webView.GoForward();
                break;
            case BrowserCommand.Reload:
                var urlStr = (DataContext as MainViewModel)?.AddressBarUrl ?? webView.Source?.ToString();
                if (!string.IsNullOrWhiteSpace(urlStr)
                    && Uri.TryCreate(urlStr, UriKind.Absolute, out var reloadUri))
                {
                    var reloadTabKey = TabKey(_activeWorkspaceId, reloadUri);
                    var navVer = _navigationVersions.GetValueOrDefault(reloadTabKey) + 1;
                    _navigationVersions[reloadTabKey] = navVer;
                    _ = NavigatePortWebViewAsync(reloadTabKey, _activeWorkspaceId, webView, reloadUri, navVer);
                }
                break;
        }
    }

    private void UpdateAddressFromWebView(string tabKey, string workspaceId, Uri? uri)
    {
        if (uri is null) return;
        if (uri.Scheme is not ("http" or "https")) return;
        UpdateAddressFromWebView(tabKey, workspaceId, uri.ToString());
    }

    private void UpdateAddressFromWebView(string tabKey, string workspaceId, string navigatedUrl)
    {
        var previousUrl = _lastKnownBrowserUrls.GetValueOrDefault(tabKey);
        _lastKnownBrowserUrls[tabKey] = navigatedUrl;
        BrowserUrlStore.Write(workspaceId, navigatedUrl);

        if (tabKey != _activeTabKey || DataContext is not MainViewModel vm)
            return;

        var addressHasUserEdit =
            FocusManager?.GetFocusedElement() == AddressBar
            && !string.Equals(vm.AddressBarUrl, previousUrl, StringComparison.Ordinal);

        if (!addressHasUserEdit)
            vm.UpdateAddressFromBrowser(workspaceId, navigatedUrl);
    }

    private async Task PollActiveBrowserAddressAsync()
    {
        if (_isClosed) return;
        if (_activeTabKey is null || _activeWorkspaceId is null) return;
        if (DataContext is not MainViewModel { ShowPortView: true }) return;
        if (!_webViews.TryGetValue(_activeTabKey, out var webView)) return;
        if (!webView.IsVisible) return;

        try
        {
            var result = await webView.InvokeScript("window.location.href");
            var url = TryReadHttpLocation(result);
            if (url is not null)
                UpdateAddressFromWebView(_activeTabKey, _activeWorkspaceId, url);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }

    private void OnAddressPollTimerTick(object? sender, EventArgs e)
        => _ = PollActiveBrowserAddressAsync();

    private void RefreshConsoleWebView()
    {
        if (_isClosed) return;
        if (DataContext is not MainViewModel vm) return;

        try
        {
            if (_consoleWebView is null)
            {
                var wv = WebViewFactory();
                var firstNavDone = false;
                wv.NavigationCompleted += (_, e) =>
                {
                    if (!e.IsSuccess) return;
                    _ = wv.InvokeScript("var t=document.getElementById('content');if(t)t.scrollTop=t.scrollHeight;");
                    if (firstNavDone) return;
                    firstNavDone = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (!ReferenceEquals(_consoleWebView, wv) || !wv.IsVisible) return;
                        wv.IsVisible = false;
                        Dispatcher.UIThread.Post(
                            () => { if (ReferenceEquals(_consoleWebView, wv)) wv.IsVisible = true; },
                            DispatcherPriority.Background);
                    });
                };
                ConsolePane.Children.Add(wv);
                _consoleWebView = wv;

                var overlay = new Panel
                {
                    Background = Brushes.Transparent,
                    Cursor = new Cursor(StandardCursorType.Ibeam),
                    Focusable = true,
                };
                overlay.PointerPressed += OnConsoleOverlayPointerPressed;
                overlay.PointerMoved += OnConsoleOverlayPointerMoved;
                overlay.PointerReleased += OnConsoleOverlayPointerReleased;
                overlay.PointerWheelChanged += OnConsoleOverlayWheelChanged;
                overlay.KeyDown += OnConsoleOverlayKeyDown;
                ConsolePane.Children.Add(overlay);
                _consoleOverlay = overlay;
            }

            IEnumerable<string> linesToShow = vm.Console.ShowAllLines
                ? vm.Console.Lines
                : vm.Console.Lines.Skip(Math.Max(0, vm.Console.Lines.Count - ConsoleDefaultDisplayLines));

            var html = BuildConsoleHtml(linesToShow);
            var htmlPath = ConsoleHtmlPath();
            File.WriteAllText(htmlPath, html, Encoding.UTF8);
            NavigateWebView(_consoleWebView, new Uri("file://" + htmlPath));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Trace.TraceWarning($"Could not load console WebView: {ex.Message}");
            if (_consoleOverlay is not null)
            {
                try { ConsolePane.Children.Remove(_consoleOverlay); } catch (InvalidOperationException rex) { Trace.TraceWarning(rex.Message); }
                _consoleOverlay = null;
            }
            _consoleWebView = null;
        }
    }

    private void DestroyConsoleWebView()
    {
        if (_consoleOverlay is not null)
        {
            try { ConsolePane.Children.Remove(_consoleOverlay); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
            _consoleOverlay = null;
        }
        if (_consoleWebView is null) return;
        try { _consoleWebView.Stop(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
        try { ConsolePane.Children.Remove(_consoleWebView); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
        if (_consoleWebView is IDisposable disposable)
            try { disposable.Dispose(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
        _consoleWebView = null;
        try { File.Delete(ConsoleHtmlPath()); } catch (IOException ex) { Trace.TraceWarning(ex.Message); }
    }

    private static readonly string _consoleHtmlPath =
        Path.Join(Path.GetTempPath(), $"agentup-console-{Environment.ProcessId}.html");

    private static string ConsoleHtmlPath() => _consoleHtmlPath;

    private static string BrowserErrorHtmlPath(string workspaceId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workspaceId)));
        return Path.Join(Path.GetTempPath(), $"agentup-browser-error-{hash[..16]}.html");
    }

    private static Uri WriteBrowserErrorPage(string workspaceId, string html)
    {
        var htmlPath = BrowserErrorHtmlPath(workspaceId);
        File.WriteAllText(htmlPath, html, Encoding.UTF8);
        return new Uri("file://" + htmlPath);
    }

    internal static string BuildBrowserErrorHtml(string title, string detail, Uri destination)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeDetail = WebUtility.HtmlEncode(detail);
        var safeUrl = WebUtility.HtmlEncode(destination.ToString());

        return $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<style>
* { box-sizing: border-box; }
html, body {
  min-height: 100%;
  margin: 0;
  background: #000000;
  color: #f5fbf7;
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}
body {
  display: grid;
  place-items: center;
  padding: 32px;
}
.panel {
  width: min(620px, 100%);
  border: 1px solid #287038;
  border-radius: 8px;
  background: #050505;
  box-shadow: 0 0 34px rgba(0, 184, 80, 0.18);
  padding: 28px;
}
h1 {
  margin: 0 0 10px;
  color: #f5fbf7;
  font-size: 30px;
  line-height: 1.1;
}
.detail {
  display: block;
  margin: 0 0 18px;
  color: #b0c8b8;
  font-size: 14px;
}
code {
  display: block;
  padding: 12px;
  border: 1px solid #184820;
  border-radius: 7px;
  background: #000000;
  color: #00d66b;
  font-family: Consolas, "Courier New", monospace;
  font-size: 12px;
  overflow-wrap: anywhere;
}
</style>
</head>
<body>
  <main class="panel">
    <h1>{{safeTitle}}</h1>
    <span class="detail">{{safeDetail}}</span>
    <code>{{safeUrl}}</code>
  </main>
</body>
</html>
""";
    }

    internal static string BuildConsoleHtml(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>");
        sb.Append("* { margin: 0; padding: 0; box-sizing: border-box; }");
        sb.Append("html, body { height: 100%; overflow: hidden; background: #000000; }");
        sb.Append("::selection { background-color: #0f7a45; color: #f5fbf7; }");
        sb.Append("#content { display: block; width: 100%; height: 100%; background: #000000; color: #c7d9d0; font-family: Consolas,'Courier New',monospace; font-size: 12px; padding: 14px 20px; white-space: pre; overflow: auto; line-height: 1.4; outline: none; cursor: text; }");
        sb.Append("</style></head><body>");
        sb.Append("<pre id=\"content\" tabindex=\"-1\">");
        foreach (var line in lines)
        {
            AppendHtmlLine(sb, line);
            sb.Append('\n');
        }
        sb.Append("</pre>");
        sb.Append("<script>");
        sb.Append(ConsoleJs);
        sb.Append("</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void AppendHtmlLine(StringBuilder sb, string line)
    {
        var i = 0;
        while (i < line.Length)
        {
            var c = line[i];
            if (c == '\x1B' && i + 1 < line.Length && line[i + 1] == '[')
            {
                i += 2;
                while (i < line.Length && !char.IsLetter(line[i]))
                    i++;
                if (i < line.Length) i++;
            }
            else
            {
                sb.Append(c switch { '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", _ => null });
                if (c is not '&' and not '<' and not '>')
                    sb.Append(c);
                i++;
            }
        }
    }

    private void DestroyWorkspaceWebViews()
    {
        foreach (var webView in _webViews.Values)
        {
            try { webView.Stop(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
            try { PortPane.Children.Remove(webView); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
            if (webView is IDisposable disposable)
            {
                try { disposable.Dispose(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
            }
        }

        _webViews.Clear();
        _webViewErrors.Clear();
        _lastKnownBrowserUrls.Clear();
        _navigationVersions.Clear();
        _agentActiveTabKeys.Clear();
        _activeWorkspaceId = null;
        _activeTabKey = null;
    }

    internal static string? TryReadHttpLocation(string? scriptResult)
    {
        scriptResult = NormalizeScriptResult(scriptResult);
        if (string.IsNullOrWhiteSpace(scriptResult)) return null;

        var candidate = scriptResult.Trim();

        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
               && uri.Scheme is "http" or "https"
            ? uri.ToString()
            : null;
    }

    internal static string? NormalizeScriptResult(string? scriptResult)
    {
        if (string.IsNullOrWhiteSpace(scriptResult)) return scriptResult;

        var candidate = scriptResult.Trim();
        if (candidate.Length < 2 || candidate[0] != '"' || candidate[^1] != '"')
            return scriptResult;

        try
        {
            return JsonSerializer.Deserialize<string>(candidate);
        }
        catch (JsonException)
        {
            return candidate[1..^1].Replace("\\/", "/").Replace("\\\"", "\"");
        }
    }

    internal static void ConfigureWebViewProfile(string workspaceId, WebViewEnvironmentRequestedEventArgs e)
    {
        var profileRoot = BrowserUrlStore.ProfilePath(workspaceId);

        switch (e)
        {
            case GtkWebViewEnvironmentRequestedEventArgs gtk:
                gtk.BaseDataDirectory = Path.Join(profileRoot, "data");
                gtk.BaseCacheDirectory = Path.Join(profileRoot, "cache");
                break;
            case LinuxWpeWebViewEnvironmentRequestedEventArgs wpe:
                wpe.DataDirectory = Path.Join(profileRoot, "data");
                wpe.CacheDirectory = Path.Join(profileRoot, "cache");
                break;
            case WindowsWebView2EnvironmentRequestedEventArgs webView2:
                webView2.UserDataFolder = Path.Join(profileRoot, "webview2");
                webView2.ProfileName = SafeProfileName(workspaceId);
                break;
            case AppleWKWebViewEnvironmentRequestedEventArgs apple:
                apple.DataStoreIdentifier = StableGuid(workspaceId);
                break;
        }
    }

    private static string SafeProfileName(string workspaceId)
    {
        var builder = new StringBuilder(workspaceId.Length);
        foreach (var c in workspaceId)
            builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');

        return builder.Length > 0 ? builder.ToString() : "workspace";
    }

    private static Guid StableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash[..16]);
    }

    // tabKey = "workspaceId:port" — uniquely identifies a persistent WebView per workspace tab.
    private static string TabKey(string workspaceId, Uri uri) => $"{workspaceId}:{uri.Port}";
    private static string WorkspaceFromTabKey(string tabKey) => tabKey[..tabKey.LastIndexOf(':')];

    private void OnConsoleOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_consoleWebView is null || _isClosed) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _consoleOverlay?.Focus();
        var pos = e.GetPosition(_consoleOverlay);
        if (e.ClickCount >= 3)
        {
            _consoleSelecting = false;
            _ = _consoleWebView.InvokeScript($"window._selLine({pos.X:F1},{pos.Y:F1})");
        }
        else if (e.ClickCount == 2)
        {
            _consoleSelecting = false;
            _ = _consoleWebView.InvokeScript($"window._selWord({pos.X:F1},{pos.Y:F1})");
        }
        else
        {
            _consoleSelecting = true;
            _ = _consoleWebView.InvokeScript($"window._selStart({pos.X:F1},{pos.Y:F1})");
        }
    }

    private void OnConsoleOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_consoleSelecting || _consoleWebView is null || _isClosed) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _consoleSelecting = false;
            return;
        }
        var pos = e.GetPosition(_consoleOverlay);
        _ = _consoleWebView.InvokeScript($"window._selExtend({pos.X:F1},{pos.Y:F1})");
    }

    private void OnConsoleOverlayPointerReleased(object? sender, PointerReleasedEventArgs e)
        => _consoleSelecting = false;

    private void OnConsoleOverlayWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_consoleWebView is null || _isClosed) return;
        var delta = -e.Delta.Y * 60.0;
        _ = _consoleWebView.InvokeScript($"window._scroll({delta:F1})");
    }

    private async void OnConsoleOverlayKeyDown(object? sender, KeyEventArgs e)
    {
        var copyModifier = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? KeyModifiers.Meta
            : KeyModifiers.Control;
        if (e.Key != Key.C || !e.KeyModifiers.HasFlag(copyModifier)) return;
        if (_consoleWebView is null || _isClosed) return;
        e.Handled = true;
        try
        {
            var result = await _consoleWebView.InvokeScript(
                "(function(){var s=window.getSelection();return s?s.toString():'';})()");
            var text = NormalizeScriptResult(result);
            if (!string.IsNullOrEmpty(text))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is not null)
                    await clipboard.SetTextAsync(text);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (FocusManager?.GetFocusedElement() != AddressBar) return;
        if (e.Source is not Visual source) return;
        if (ReferenceEquals(source, AddressBar) || AddressBar.IsVisualAncestorOf(source)) return;

        FocusSink.Focus(NavigationMethod.Pointer);
    }

    private void OnWindowChromePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is Visual source && IsWindowControlSource(source)) return;

        if (e.ClickCount == 2)
            ToggleMaximized();
        else
            BeginMoveDrag(e);
    }

    private bool IsWindowControlSource(Visual source)
        => CloseWindowButton.IsVisualAncestorOf(source)
           || MinimizeWindowButton.IsVisualAncestorOf(source)
           || RestoreWindowButton.IsVisualAncestorOf(source)
           || SidebarToggle.IsVisualAncestorOf(source)
           || ReloadButton.IsVisualAncestorOf(source);

    private void OnCloseWindowClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnMinimizeWindowClicked(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnRestoreWindowClicked(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void ToggleMaximized()
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnOpenTutorialFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { Tutorial.ProjectDirectory: { Length: > 0 } path }) return;
        if (!Directory.Exists(path)) return;

        var (fileName, arguments) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("explorer.exe", $"\"{path}\"")
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? ("open", $"\"{path}\"")
                : ("xdg-open", $"\"{path}\"");

        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }
}
