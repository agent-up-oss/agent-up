using System.Diagnostics;
using System.Collections.Specialized;
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
using AgentUp.Desktop.Features.Audit.Controllers;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    // One NativeWebView per tab — keyed by "workspaceId:viewer" (AI stream) or "workspaceId:{port}" (human direct).
    // Switching between workspace tabs only toggles IsVisible; the WebView is never navigated away,
    // preserving full page state (scroll position, open accordions, JS memory, auth session).
    private readonly Dictionary<string, NativeWebView> _webViews = new();
    // Errors keyed by workspaceId (not tabKey) so the banner persists across tab switches.
    private readonly Dictionary<string, string> _webViewErrors = new();
    // Last successfully navigated http URL per tabKey; absent means tab is in error state.
    private readonly Dictionary<string, string> _lastKnownBrowserUrls = new();
    private readonly Dictionary<string, int> _navigationVersions = new();
    // Current control authority per workspace: "ai" (default) or "human".
    private readonly Dictionary<string, string> _workspaceAuthority = new();
    // Server-authoritative stream state per workspace. This is the *only* signal the UI
    // consults to decide "show viewer WebView vs show a status banner". Populated by the
    // stream-state SSE event; RenderStreamState is the sole writer of viewer.IsVisible.
    private readonly Dictionary<string, StreamStateSnapshot> _streamStates = new();
    // Last kind actually rendered per workspace, so transitions INTO Streaming can trigger
    // a WebView reload (reconnect the RDP WebSocket) without renavigating on every render.
    private readonly Dictionary<string, StreamStateKind?> _lastRenderedKind = new();
    // Tracks which workspaces have had their viewer page complete at least one navigation,
    // so RenderStreamState can ReloadWebView (reconnect WebSocket) vs NavigateWebView (first load).
    private readonly HashSet<string> _viewerPagesLoaded = new();
    private DateTimeOffset _lastHeadlessRetry = DateTimeOffset.MinValue;
    // Tracks OS-window focus. Drives per-viewer presence: an unfocused window means the
    // user isn't looking, so every viewer drops to background (1 fps) on the server side.
    // Default true because Avalonia's Activated event may not fire before the first render
    // if the window opens focused (common case) — assuming true avoids a startup 1 fps spike.
    private bool _windowFocused = true;
    // Poll of window.__viewer.snapshot() on the active viewer WebView. See
    // OnViewerSnapshotPollTick — this is the only bridge from the JS state machine into
    // Avalonia's state machine. Every ~500 ms it reads the snapshot, updates
    // _viewerSnapshots for the active workspace, and lets RenderStreamState react.
    private readonly DispatcherTimer _viewerSnapshotPollTimer;
    // Last-known JS state machine snapshot per workspace, keyed by workspaceId. Only
    // the active workspace's snapshot is refreshed (that's the only viewer currently
    // showing content to the user); non-active viewers are considered stale.
    private readonly Dictionary<string, ViewerSnapshot> _viewerSnapshots = new();
    private readonly CompositeDisposable _subscriptions = new();
    private readonly DispatcherTimer _addressPollTimer;
    private readonly HttpClient _serverHttp;
    private readonly string _serverBaseUrl;
    private WorkspaceEventClient? _workspaceEventClient;
    private BrowserEventClient? _browserEventClient;
    private bool _hadBrowserEventsConnection;
    private bool _browserEventsConnected;
    private string? _activeWorkspaceId;
    private string? _activeTabKey;   // tabKey of the currently visible WebView
    private bool _isClosed;
    private NativeWebView? _consoleWebView;
    private Panel? _consoleOverlay;
    private bool _consoleSelecting;
    private ViewModelAuditController? _auditController;
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
        || HasWorkspaceBrowserResourcesForTests
        || _activeWorkspaceId is not null;
    internal bool HasWorkspaceBrowserResourcesForTests =>
        _webViews.Count > 0
        || _webViewErrors.Count > 0
        || _lastKnownBrowserUrls.Count > 0
        || _workspaceAuthority.Count > 0
        || _activeWorkspaceId is not null
        || _activeTabKey is not null;

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
        _viewerSnapshotPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _viewerSnapshotPollTimer.Tick += OnViewerSnapshotPollTick;
        _viewerSnapshotPollTimer.Start();
        PortPane.SizeChanged += OnPortPaneSizeChanged;
        Activated += (_, _) =>
        {
            _windowFocused = true;
            UpdateBackgroundAttentionOverlay();
            UpdateAllViewerPresences();
        };
        Deactivated += (_, _) =>
        {
            _windowFocused = false;
            UpdateBackgroundAttentionOverlay();
            UpdateAllViewerPresences();
        };
        var serverUrl = Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL") ?? "http://localhost:5000";
        _serverBaseUrl = serverUrl;
        _serverHttp = new HttpClient { BaseAddress = new Uri(serverUrl) };
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
        _workspaceEventClient?.Dispose();
        _workspaceEventClient = null;
        _browserEventClient?.Dispose();
        _browserEventClient = null;

        base.OnDataContextChanged(e);
        if (DataContext is not MainViewModel vm) return;

        var eventHttp = new HttpClient { BaseAddress = _serverHttp.BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
        _workspaceEventClient = new WorkspaceEventClient(eventHttp, vm.Sidebar);
        _workspaceEventClient.Start();

        var browserEventHttp = new HttpClient { BaseAddress = _serverHttp.BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
        _browserEventClient = new BrowserEventClient(browserEventHttp);
        _browserEventClient.Connected += OnBrowserEventsConnected;
        _browserEventClient.Disconnected += OnBrowserEventsDisconnected;
        _browserEventClient.StreamStateChanged += OnStreamStateChanged;
        _browserEventClient.Start();

        _subscriptions.Clear();
        vm.BrowserNavigation.Subscribe(nav =>
            Dispatcher.UIThread.Post(() => HandleNavigation(nav.WorkspaceId, nav.Url, reloadIfSameUrl: true)))
            .DisposeWith(_subscriptions);
        vm.BrowserTabNavigation.Subscribe(nav =>
            Dispatcher.UIThread.Post(() => HandleNavigation(nav.WorkspaceId, nav.Url, reloadIfSameUrl: false)))
            .DisposeWith(_subscriptions);
        vm.BrowserCommands.Subscribe(command =>
            Dispatcher.UIThread.Post(() => HandleBrowserCommand(command)))
            .DisposeWith(_subscriptions);
        vm.Sidebar.Workspaces.CollectionChanged += OnWorkspaceCollectionChanged;
        Disposable.Create(() => vm.Sidebar.Workspaces.CollectionChanged -= OnWorkspaceCollectionChanged)
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
        vm.WhenAnyValue(v => v.ShowPortView)
            .Skip(1)
            .DistinctUntilChanged()
            .Where(show => show)
            .Subscribe(_ => Dispatcher.UIThread.Post(WakeActiveViewer))
            .DisposeWith(_subscriptions);
        // Sub-tab changes (viewer ↔ console ↔ port) flip which viewer is user-visible,
        // so every viewer needs its presence recomputed — the one becoming active goes
        // foreground, the one leaving goes background.
        vm.WhenAnyValue(v => v.SelectedSubTab)
            .Skip(1)
            .Subscribe(_ => Dispatcher.UIThread.Post(UpdateAllViewerPresences))
            .DisposeWith(_subscriptions);

        _auditController ??= new ViewModelAuditController(_serverHttp);
        _auditController.Attach(vm, CaptureViewState);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _workspaceEventClient?.Dispose();
        _browserEventClient?.Dispose();
        _auditController?.Dispose();
        _serverHttp.Dispose();
        _addressPollTimer.Stop();
        _addressPollTimer.Tick -= OnAddressPollTimerTick;
        _viewerSnapshotPollTimer.Stop();
        _viewerSnapshotPollTimer.Tick -= OnViewerSnapshotPollTick;
        _subscriptions.Dispose();
        DestroyWorkspaceWebViews();
        DestroyConsoleWebView();
        base.OnClosed(e);
    }

    private IReadOnlyDictionary<string, string> CaptureViewState()
    {
        if (Dispatcher.UIThread.CheckAccess())
            return CaptureCoreOnUiThread();
        try
        {
            return Dispatcher.UIThread.Invoke(CaptureCoreOnUiThread);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or TaskCanceledException)
        {
            return new Dictionary<string, string> { ["webView.captureError"] = ex.Message };
        }
    }

    private Dictionary<string, string> CaptureCoreOnUiThread()
    {
        var f = new Dictionary<string, string>
        {
            ["webView.activeWorkspaceId"] = _activeWorkspaceId ?? string.Empty,
            ["webView.activeTabKey"] = _activeTabKey ?? string.Empty,
            ["webView.webViewCount"] = _webViews.Count.ToString(),
            ["webView.hasConsoleWebView"] = (_consoleWebView is not null).ToString(),
            ["webView.windowState"] = WindowState.ToString(),
            ["webView.isClosed"] = _isClosed.ToString(),
            ["webView.addressPollTimerEnabled"] = _addressPollTimer.IsEnabled.ToString(),
            ["webView.errorCount"] = _webViewErrors.Count.ToString(),
            ["webView.errors"] = string.Join("; ", _webViewErrors.Select(kv => $"{kv.Key}={kv.Value}")),
            ["webView.lastKnownUrlCount"] = _lastKnownBrowserUrls.Count.ToString(),
            ["webView.lastKnownUrls"] = string.Join("; ", _lastKnownBrowserUrls.Select(kv => $"{kv.Key}={kv.Value}")),
            ["webView.tabKeys"] = string.Join(", ", _webViews.Keys),
        };

        if (_activeTabKey is not null && _webViews.TryGetValue(_activeTabKey, out var activeWv))
            f["webView.activeSourceUrl"] = activeWv.Source?.ToString() ?? string.Empty;
        else
            f["webView.activeSourceUrl"] = string.Empty;

        // Diagnostic fields so audit can distinguish "modal not firing" from "modal firing
        // but hidden by native z-order". If windowFocused=false + streamState=Streaming +
        // backgroundAttentionBannerVisible=true, and the user still reports no modal, the
        // banner IS being drawn — it just can't beat the native GTK/WebKit subwindow to
        // the compositor layer.
        f["webView.windowFocused"] = _windowFocused.ToString();
        f["webView.backgroundAttentionBannerVisible"] = BackgroundAttentionBanner.IsVisible.ToString();
        f["webView.browserConnectingBannerVisible"] = BrowserConnectingBanner.IsVisible.ToString();
        f["webView.chromiumDownloadBannerVisible"] = ChromiumDownloadBanner.IsVisible.ToString();
        if (_activeWorkspaceId is not null && _viewerSnapshots.TryGetValue(_activeWorkspaceId, out var vs))
        {
            f["webView.jsSmState"] = vs.State;
            f["webView.jsSmStateAgeMs"] = ((long)vs.StateAge.TotalMilliseconds).ToString();
            f["webView.jsSmFramesReceived"] = vs.FramesReceived.ToString();
            f["webView.jsSmWsReadyState"] = vs.WsReadyState;
            f["webView.jsSmPresence"] = vs.Presence;
        }
        else
        {
            f["webView.jsSmState"] = "";
            f["webView.jsSmStateAgeMs"] = "";
            f["webView.jsSmFramesReceived"] = "";
            f["webView.jsSmWsReadyState"] = "";
            f["webView.jsSmPresence"] = "";
        }
        if (_activeTabKey is not null && _webViews.TryGetValue(_activeTabKey, out var activeMarginWv))
        {
            f["webView.activeMargin"] = activeMarginWv.Margin.ToString();
            f["webView.activeBounds"] = $"{activeMarginWv.Bounds.Width:F0}x{activeMarginWv.Bounds.Height:F0}";
            f["webView.activeDesiredSize"] = $"{activeMarginWv.DesiredSize.Width:F0}x{activeMarginWv.DesiredSize.Height:F0}";
            f["webView.activeIsHitTestVisible"] = activeMarginWv.IsHitTestVisible.ToString();
            f["webView.activeMaxSize"] = $"{activeMarginWv.MaxWidth}x{activeMarginWv.MaxHeight}";
        }
        else
        {
            f["webView.activeMargin"] = "";
            f["webView.activeBounds"] = "";
            f["webView.activeDesiredSize"] = "";
            f["webView.activeIsHitTestVisible"] = "";
            f["webView.activeMaxSize"] = "";
        }
        if (_activeWorkspaceId is not null && _streamStates.TryGetValue(_activeWorkspaceId, out var activeSnap))
            f["webView.activeStreamKind"] = activeSnap.Kind.ToString();
        else
            f["webView.activeStreamKind"] = "None";

        return f;
    }

    private void OnWorkspaceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isClosed) return;

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            DestroyWorkspaceWebViews();
            return;
        }

        if (e.OldItems is null) return;
        foreach (var item in e.OldItems.OfType<WorkspaceItemViewModel>())
            DestroyWorkspaceWebViews(item.Id);
    }

    internal void NavigateTo(string workspaceId, string? url) => HandleNavigation(workspaceId, url, reloadIfSameUrl: true);

    // Evaluates a script in the tab the agent last navigated to for the given workspace.
    internal async Task<string?> EvalAsync(string workspaceId, string script)
    {
        var result = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var tabKey = ResolveEvaluationTabKey(workspaceId);
            if (tabKey is null) return null;
            if (!_webViews.TryGetValue(tabKey, out var webView)) return null;
            return await webView.InvokeScript(script);
        });
        return NormalizeScriptResult(result);
    }

    private string? ResolveEvaluationTabKey(string workspaceId)
        => _activeWorkspaceId == workspaceId ? _activeTabKey : null;

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

    private void HandleNavigation(string? workspaceId, string? url, bool reloadIfSameUrl)
    {
        if (_isClosed || workspaceId is null) return;
        var authority = _workspaceAuthority.GetValueOrDefault(workspaceId, "ai");
        if (authority == "human")
            HandleDirectNavigation(workspaceId, url, IsTutorialVisible(), reloadIfSameUrl);
        else
            HandleHeadlessNavigation(workspaceId, url, IsTutorialVisible(), reloadIfSameUrl);
    }

    private void HandleDirectNavigation(string? workspaceId, string? url, bool tutorialVisible, bool reloadIfSameUrl)
    {
        if (workspaceId is null || url is null) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var navUri) || navUri.Scheme is not ("http" or "https")) return;

        var tabKey = TabKey(workspaceId, navUri);
        ActivateTab(workspaceId, tabKey, tutorialVisible);

        if (_webViews.TryGetValue(tabKey, out var existingWebView))
        {
            existingWebView.IsVisible = !tutorialVisible;
            if (!reloadIfSameUrl && !ShouldNavigateExistingWebView(_lastKnownBrowserUrls.GetValueOrDefault(tabKey), url))
                return;
            var errNavVer = _navigationVersions.GetValueOrDefault(tabKey) + 1;
            _navigationVersions[tabKey] = errNavVer;
            _ = NavigatePortWebViewAsync(tabKey, workspaceId, existingWebView, navUri, errNavVer);
            return;
        }

        if (!TryGetOrCreateWebView(tabKey, workspaceId, url, out var webView, out var destinationUrl)) return;
        webView.IsVisible = !tutorialVisible;
        var navigationVersion = _navigationVersions.GetValueOrDefault(tabKey) + 1;
        _navigationVersions[tabKey] = navigationVersion;
        _ = NavigatePortWebViewAsync(tabKey, workspaceId, webView, new Uri(destinationUrl), navigationVersion);
    }

    private static string TabKey(string workspaceId, Uri uri) => $"{workspaceId}:{uri.Port}";

    internal static bool ShouldNavigateExistingWebView(string? lastKnownUrl, string requestedUrl)
        => lastKnownUrl is null || !string.Equals(lastKnownUrl, requestedUrl, StringComparison.Ordinal);

    internal static bool ShouldReclaimViewerUrl(string? currentSource, string viewerUrl)
        => !string.Equals(currentSource, viewerUrl, StringComparison.Ordinal);

    internal static bool IsBrowserViewerRequest(Uri? request)
        => request?.AbsolutePath == "/api/browser/rdp-viewer";

    private void ActivateTab(string? workspaceId, string? tabKey, bool tutorialVisible)
    {
        if (workspaceId == _activeWorkspaceId && tabKey == _activeTabKey) return;

        var previousWorkspaceId = _activeWorkspaceId;
        if (_activeTabKey is not null && _webViews.TryGetValue(_activeTabKey, out var previous))
            previous.IsVisible = false;

        _activeWorkspaceId = workspaceId;
        _activeTabKey = tabKey;

        // For viewer tabs, visibility is decided by RenderStreamState — NOT unconditionally
        // shown. Only human-mode direct-port tabs get their WebView shown here directly.
        var isViewerTab = tabKey is not null && tabKey.EndsWith(":viewer", StringComparison.Ordinal);
        if (!tutorialVisible && !isViewerTab && tabKey is not null && _webViews.TryGetValue(tabKey, out var next))
            next.IsVisible = true;

        UpdateErrorDisplay(workspaceId);

        // Refresh stream-state rendering: the previously-active viewer needs its banner cleared,
        // and the newly-active viewer's WebView/banner needs to match the current stream state.
        if (previousWorkspaceId is not null && previousWorkspaceId != workspaceId)
            RenderStreamState(previousWorkspaceId);
        if (workspaceId is not null)
            RenderStreamState(workspaceId);
        UpdateBackgroundAttentionOverlay();
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

            // Start hidden; HandleNavigation makes it visible as needed.
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

        webView.NavigationCompleted += (_, e) =>
        {
            var url = e.Request?.ToString() ?? string.Empty;
            if (!e.IsSuccess)
            {
                RecordWebViewEvent(workspaceId, "navigation_error", "error", new()
                {
                    ["tabKey"] = tabKey,
                    ["url"] = url,
                    ["streamKind"] = _streamStates.GetValueOrDefault(workspaceId)?.Kind.ToString() ?? "unknown",
                    ["isVisible"] = webView.IsVisible.ToString(),
                });

                if (e.Request is { } failedUri && failedUri.Scheme is "http" or "https")
                {
                    if (IsBrowserViewerRequest(failedUri))
                    {
                        RetryViewerNavigation(tabKey, workspaceId, webView, failedUri);
                        return;
                    }

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

            RecordWebViewEvent(workspaceId, "navigation_complete", "success", new()
            {
                ["tabKey"] = tabKey,
                ["url"] = url,
                ["isVisible"] = webView.IsVisible.ToString(),
            });

            if (e.Request is { } successUri && IsBrowserViewerRequest(successUri))
            {
                _viewerPagesLoaded.Add(workspaceId);
                // Page's __setPresence hook now exists — push the desktop's current view
                // of foreground/background so the fresh JS doesn't default-report itself
                // as foreground when the user is actually elsewhere.
                UpdateViewerPresence(workspaceId);
            }

            _ = webView.InvokeScript(SelectionJs);
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
            return;

        webView.Source = destination;
    }

    private static void ReloadWebView(NativeWebView webView, Uri destination)
    {
        webView.Source = new Uri("about:blank");
        Dispatcher.UIThread.Post(() => webView.Source = destination, DispatcherPriority.Background);
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
            ChromiumDownloadBanner.IsVisible = false;
            BrowserConnectingBanner.IsVisible = false;
            return;
        }
        UpdateErrorDisplay(_activeWorkspaceId);
        if (_activeTabKey is null) return;
        if (!_webViews.TryGetValue(_activeTabKey, out var active)) return;
        if (DataContext is not MainViewModel { ShowPortView: true }) return;

        // For viewer tabs, defer to RenderStreamState (invariant: sole writer of viewer.IsVisible).
        var isViewerTab = _activeTabKey.EndsWith(":viewer", StringComparison.Ordinal);
        if (isViewerTab && _activeWorkspaceId is not null)
            RenderStreamState(_activeWorkspaceId);
        else
            active.IsVisible = true;
    }

    private void HandleBrowserCommand(BrowserCommand command)
    {
        if (_isClosed || _activeWorkspaceId is null) return;

        if (_workspaceAuthority.GetValueOrDefault(_activeWorkspaceId, "ai") == "human")
        {
            if (_activeTabKey is not null && _webViews.TryGetValue(_activeTabKey, out var wv))
            {
                switch (command)
                {
                    case BrowserCommand.Back: wv.GoBack(); break;
                    case BrowserCommand.Forward: wv.GoForward(); break;
                    case BrowserCommand.Reload:
                        if (wv.Source is { } src) ReloadWebView(wv, src);
                        break;
                }
            }
            return;
        }

        _ = PostHeadlessBrowserCommandAsync(command, _activeWorkspaceId);
    }

    private async Task PollActiveBrowserAddressAsync()
    {
        if (_isClosed) return;
        if (_activeWorkspaceId is null) return;
        if (DataContext is not MainViewModel { ShowPortView: true }) return;
        await PollHeadlessAddressAsync(_activeWorkspaceId);
        await PollControlModeAsync(_activeWorkspaceId);
    }

    private void OnBrowserEventsConnected()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_isClosed) return;
            _hadBrowserEventsConnection = true;
            _browserEventsConnected = true;
            ConnectionLostBanner.IsVisible = false;
        });
    }

    private void OnBrowserEventsDisconnected()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_isClosed) return;
            _browserEventsConnected = false;
            if (!_hadBrowserEventsConnection) return;
            ConnectionLostBanner.IsVisible = true;
            ChromiumDownloadBanner.IsVisible = false;
            BrowserConnectingBanner.IsVisible = false;
            // SSE stream is down — server-side truth is stale. Purge cached state so
            // RenderStreamState hides the WebView until reconnection replays the events.
            _streamStates.Clear();
            _lastRenderedKind.Clear();
            var viewerKey = _activeWorkspaceId is not null ? $"{_activeWorkspaceId}:viewer" : null;
            if (viewerKey is not null && _webViews.TryGetValue(viewerKey, out var viewer))
                viewer.IsVisible = false;
        });
    }

    private void OnStreamStateChanged(StreamStateSnapshot snapshot)
    {
        RecordWebViewEvent(snapshot.WorkspaceId, "stream_state_changed", snapshot.Kind.ToString(), new()
        {
            ["kind"] = snapshot.Kind.ToString(),
            ["chromiumState"] = snapshot.ChromiumState ?? string.Empty,
            ["chromiumProgress"] = snapshot.ChromiumProgress.ToString(),
            ["attempt"] = snapshot.Attempt.ToString(),
            ["maxAttempts"] = snapshot.MaxAttempts.ToString(),
            ["isActiveWorkspace"] = (_activeWorkspaceId == snapshot.WorkspaceId).ToString(),
        });

        Dispatcher.UIThread.Post(() =>
        {
            if (_isClosed) return;
            _streamStates[snapshot.WorkspaceId] = snapshot;
            RenderStreamState(snapshot.WorkspaceId);
        });
    }

    // Modal is now purely derived from JS state machine snapshot + focus. Shown only
    // when the JS SM says it has been stalled for a meaningful period AND the window
    // is unfocused — the two conditions together mean: "the AI is expected to be doing
    // something, WebKit isn't painting fresh frames, and the user isn't looking, so
    // what they'd see if they glanced over is stale." Otherwise hidden.
    private static readonly TimeSpan ModalStalledThreshold = TimeSpan.FromSeconds(5);
    private void UpdateBackgroundAttentionOverlay()
    {
        if (_isClosed) return;

        var snapshot = _activeWorkspaceId is not null
            ? _viewerSnapshots.GetValueOrDefault(_activeWorkspaceId)
            : null;
        var stalledLongEnough = snapshot is not null
            && string.Equals(snapshot.State, "stalled", StringComparison.Ordinal)
            && DateTimeOffset.UtcNow - snapshot.ObservedAt < TimeSpan.FromSeconds(2)
            && snapshot.StateAge >= ModalStalledThreshold;

        var streamKind = _activeWorkspaceId is not null
            ? _streamStates.GetValueOrDefault(_activeWorkspaceId)?.Kind
            : null;
        var streamWantsToRender = streamKind is StreamStateKind.Streaming
            or StreamStateKind.SessionLaunching;

        var shouldShow = !_windowFocused && streamWantsToRender && stalledLongEnough;
        BackgroundAttentionText.Text = "AI browser view is stale";
        BackgroundAttentionBanner.IsVisible = shouldShow;

        // Best-effort taskbar-flash the first time we enter the "user should know" state
        // while unfocused. Modern Linux/macOS WMs treat Activate() on a background
        // window as an urgency hint rather than a focus-steal.
        if (shouldShow && !_lastFrameAttentionShown)
        {
            try { Activate(); }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
            {
                // No cross-platform "request attention" API in Avalonia 12; if the
                // platform rejects Activate() on a background window, silently no-op —
                // the modal itself is still shown and is the primary user signal.
                Trace.TraceWarning(ex.Message);
            }
        }
        _lastFrameAttentionShown = shouldShow;
    }
    private bool _lastFrameAttentionShown;

    // ────────────────────────────────────────────────────────────────
    // INVARIANT: this is the ONLY method that writes viewer.IsVisible
    // for the AI-stream WebView, and the only method that toggles the
    // ChromiumDownloadBanner or BrowserConnectingBanner. Any change to
    // "when do we show the WebView vs a banner" must live here.
    // Callers: OnStreamStateChanged (SSE), ActivateTab, tutorial-visibility
    // change, workspace destroy, SwitchWebViewMode.
    // ────────────────────────────────────────────────────────────────
    private void RenderStreamState(string workspaceId)
    {
        if (_isClosed) return;

        var viewerKey = $"{workspaceId}:viewer";
        var isAi = _workspaceAuthority.GetValueOrDefault(workspaceId, "ai") == "ai";
        var isActiveViewerTab = _activeWorkspaceId == workspaceId && _activeTabKey == viewerKey;
        var tutorialVisible = IsTutorialVisible();
        _streamStates.TryGetValue(workspaceId, out var snap);
        var kind = snap?.Kind;
        var prevKind = _lastRenderedKind.GetValueOrDefault(workspaceId);
        // Visibility follows tab activation ONLY — never gated by stream state. If the
        // active viewer's stream flips to non-Streaming we keep the WebView mapped so
        // WebKit doesn't unmap the GTK widget (that unmap froze the compositor + timers
        // for minutes on re-map, producing the "blank screen" bug). When a banner should
        // occlude the WebView we push it offscreen via Margin (see PositionWebView)
        // because Linux/GTK renders native subwindows above Avalonia content regardless
        // of ZIndex, so a straight overlay would be hidden behind the WebView surface.
        var showWebView = isAi && isActiveViewerTab && !tutorialVisible;

        if (_webViews.TryGetValue(viewerKey, out var viewer))
        {
            viewer.IsVisible = showWebView;
            if (showWebView && kind == StreamStateKind.Streaming)
            {
                var viewerUrl = BuildViewerUrl(workspaceId);
                var enteringStream = prevKind != StreamStateKind.Streaming;
                if (enteringStream || !IsAtViewerUrl(viewer, viewerUrl))
                {
                    // Defer navigation one dispatcher tick past Loaded so any layout the
                    // banner-hide triggered has settled before WebKit's page-load runs.
                    var pinnedViewer = viewer;
                    var pinnedUrl = viewerUrl;
                    var shouldReload = _viewerPagesLoaded.Contains(workspaceId);
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_isClosed) return;
                        if (!_webViews.TryGetValue(viewerKey, out var current) || !ReferenceEquals(current, pinnedViewer))
                            return;
                        if (shouldReload) ReloadWebView(pinnedViewer, pinnedUrl);
                        else NavigateWebView(pinnedViewer, pinnedUrl);
                    }, DispatcherPriority.Loaded);
                }
            }
        }

        // Banner state applies only when this workspace's viewer tab is currently active.
        if (isAi && isActiveViewerTab && !tutorialVisible)
            ApplyBanners(snap);
        else if (_activeWorkspaceId == workspaceId)
        {
            ChromiumDownloadBanner.IsVisible = false;
            BrowserConnectingBanner.IsVisible = false;
        }

        _lastRenderedKind[workspaceId] = kind;
        UpdateViewerPresence(workspaceId);
        if (workspaceId == _activeWorkspaceId)
            UpdateBackgroundAttentionOverlay();
    }

    // Fires every 500 ms while the app is alive. Reads the JS state machine snapshot
    // on the active viewer WebView and feeds it back into Avalonia's SM via
    // OnViewerSnapshot. This is the ONLY bridge from JS → Avalonia — no ad-hoc pokes,
    // no assumptions, one polling read per tick. Side-effect bonus: the InvokeScript
    // call keeps WebKit's JS event loop ticking, so background-page throttling can't
    // freeze the JS runtime the way it used to.
    private async void OnViewerSnapshotPollTick(object? sender, EventArgs e)
    {
        if (_isClosed || _activeWorkspaceId is null || _activeTabKey is null) return;
        if (!_activeTabKey.EndsWith(":viewer", StringComparison.Ordinal)) return;
        if (!_webViews.TryGetValue(_activeTabKey, out var viewer)) return;
        if (!_viewerPagesLoaded.Contains(_activeWorkspaceId)) return;

        string? raw;
        try
        {
            raw = await viewer.InvokeScript(
                "(window.__viewer && window.__viewer.snapshot) ? JSON.stringify(window.__viewer.snapshot()) : null");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return;
        }
        if (_isClosed) return;
        var snapshot = ViewerSnapshot.TryParse(raw);
        if (snapshot is null) return;
        OnViewerSnapshot(_activeWorkspaceId, snapshot);
    }

    // Reducer that lets the JS SM feed the Avalonia SM. Called once per successful
    // snapshot poll on the active viewer. Updates observable state and triggers the
    // dependent UI refreshes.
    private void OnViewerSnapshot(string workspaceId, ViewerSnapshot snapshot)
    {
        _viewerSnapshots[workspaceId] = snapshot;
        if (workspaceId == _activeWorkspaceId)
            UpdateBackgroundAttentionOverlay();
    }

    // Presence is "foreground" only if a human is actually watching THIS viewer right now:
    // window has focus, viewer tab is the active sub-tab, workspace is the active workspace,
    // AI mode, no tutorial in the way, AND stream state is Streaming (non-Streaming means
    // banner is covering it or content is stale, no point burning CPU sending frames).
    // Anything else → background → server caps this subscriber at 1 fps.
    private void UpdateViewerPresence(string workspaceId)
    {
        if (_isClosed) return;
        var viewerKey = $"{workspaceId}:viewer";
        if (!_webViews.TryGetValue(viewerKey, out var viewer)) return;
        if (!_viewerPagesLoaded.Contains(workspaceId)) return;  // page's __setPresence hook not yet installed.

        var isAi = _workspaceAuthority.GetValueOrDefault(workspaceId, "ai") == "ai";
        var isActiveViewerTab = _activeWorkspaceId == workspaceId && _activeTabKey == viewerKey;
        var tutorialVisible = IsTutorialVisible();
        var streamStreaming = _streamStates.GetValueOrDefault(workspaceId)?.Kind == StreamStateKind.Streaming;
        var isForeground = _windowFocused && isAi && isActiveViewerTab && !tutorialVisible && streamStreaming;

        var state = isForeground ? "foreground" : "background";
        _ = viewer.InvokeScript($"window.__setPresence && window.__setPresence('{state}')");
    }

    private void UpdateAllViewerPresences()
    {
        if (_isClosed) return;
        foreach (var workspaceId in _webViews.Keys
                     .Where(k => k.EndsWith(":viewer", StringComparison.Ordinal))
                     .Select(k => k[..^":viewer".Length])
                     .ToList())
            UpdateViewerPresence(workspaceId);
    }

    private void ApplyBanners(StreamStateSnapshot? snap)
    {
        if (snap is null)
        {
            ChromiumDownloadBanner.IsVisible = false;
            BrowserConnectingBanner.IsVisible = true;
            BrowserConnectingText.Text = "Connecting…";
            return;
        }

        switch (snap.Kind)
        {
            case StreamStateKind.ChromiumDownloading:
                BrowserConnectingBanner.IsVisible = false;
                ChromiumDownloadBanner.IsVisible = true;
                var failed = string.Equals(snap.ChromiumState, "failed", StringComparison.Ordinal);
                ChromiumDownloadText.Text = failed
                    ? "Chromium download failed. AI mode unavailable."
                    : snap.ChromiumProgress > 0
                        ? $"Downloading Chromium… {snap.ChromiumProgress}%"
                        : "Downloading Chromium…";
                ChromiumDownloadProgress.IsIndeterminate = !failed && snap.ChromiumProgress == 0;
                ChromiumDownloadProgress.Value = snap.ChromiumProgress;
                ChromiumDownloadProgress.IsVisible = !failed;
                break;
            case StreamStateKind.WorkspaceStopped:
                ChromiumDownloadBanner.IsVisible = false;
                BrowserConnectingBanner.IsVisible = true;
                BrowserConnectingText.Text = "Workspace stopped.";
                break;
            case StreamStateKind.AppConnecting:
                ChromiumDownloadBanner.IsVisible = false;
                BrowserConnectingBanner.IsVisible = true;
                BrowserConnectingText.Text = snap.MaxAttempts > 0 && snap.Attempt > 0
                    ? $"Connecting to app… ({snap.Attempt} / {snap.MaxAttempts})"
                    : "Connecting to app…";
                break;
            case StreamStateKind.AppFailed:
                ChromiumDownloadBanner.IsVisible = false;
                BrowserConnectingBanner.IsVisible = true;
                BrowserConnectingText.Text = snap.Reason ?? "Could not reach app.";
                break;
            case StreamStateKind.SessionLaunching:
                ChromiumDownloadBanner.IsVisible = false;
                BrowserConnectingBanner.IsVisible = true;
                BrowserConnectingText.Text = "Preparing browser session…";
                break;
            case StreamStateKind.Streaming:
                ChromiumDownloadBanner.IsVisible = false;
                BrowserConnectingBanner.IsVisible = false;
                break;
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
        foreach (var tabKey in _webViews.Keys.ToList())
            DestroyWorkspaceWebView(tabKey);

        _webViewErrors.Clear();
        _lastKnownBrowserUrls.Clear();
        _navigationVersions.Clear();
        _workspaceAuthority.Clear();
        _viewerPagesLoaded.Clear();
        _streamStates.Clear();
        _lastRenderedKind.Clear();
        _activeWorkspaceId = null;
        _activeTabKey = null;
    }

    private void DestroyWorkspaceWebViews(string workspaceId)
    {
        foreach (var tabKey in _webViews.Keys.Where(key => key.StartsWith($"{workspaceId}:", StringComparison.Ordinal)).ToList())
            DestroyWorkspaceWebView(tabKey);

        _webViewErrors.Remove(workspaceId);
        _workspaceAuthority.Remove(workspaceId);
        _viewerPagesLoaded.Remove(workspaceId);
        _streamStates.Remove(workspaceId);
        _lastRenderedKind.Remove(workspaceId);
        DeleteBrowserErrorPage(workspaceId);

        if (_activeWorkspaceId != workspaceId)
            return;

        _activeWorkspaceId = null;
        _activeTabKey = null;
        UpdateErrorDisplay(null);
    }

    private void DestroyWorkspaceWebView(string tabKey)
    {
        if (!_webViews.Remove(tabKey, out var webView))
            return;

        try { webView.Stop(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
        try { PortPane.Children.Remove(webView); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }
        if (webView is IDisposable disposable)
            try { disposable.Dispose(); } catch (InvalidOperationException ex) { Trace.TraceWarning(ex.Message); }

        _lastKnownBrowserUrls.Remove(tabKey);
        _navigationVersions.Remove(tabKey);
    }

    private static void DeleteBrowserErrorPage(string workspaceId)
    {
        try { File.Delete(BrowserErrorHtmlPath(workspaceId)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Trace.TraceWarning(ex.Message); }
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

    private void HandleHeadlessNavigation(string workspaceId, string? url, bool tutorialVisible, bool reloadIfSameUrl)
    {
        var tabKey = $"{workspaceId}:viewer";
        ActivateTab(workspaceId, tabKey, tutorialVisible);

        // Only create the viewer WebView when we actually have a URL to load. Otherwise
        // the WebView stays absent and RenderStreamState shows a banner. This keeps the
        // "no navigation yet" case free of any WebView instantiation (which can fail).
        if (url is not null
            && Uri.TryCreate(url, UriKind.Absolute, out var navUri)
            && navUri.Scheme is "http" or "https")
        {
            if (!_webViews.ContainsKey(tabKey))
            {
                var viewerUrl = BuildViewerUrl(workspaceId);
                if (!TryGetOrCreateWebView(tabKey, workspaceId, viewerUrl.ToString(), out _, out _))
                    return;
            }

            _lastKnownBrowserUrls[tabKey] = url;
            RecordWebViewEvent(workspaceId, "headless_navigate", "info", new()
            {
                ["url"] = url,
                ["reloadIfSameUrl"] = reloadIfSameUrl.ToString(),
                ["viewerTabKey"] = tabKey,
            });
            _ = PostHeadlessNavigateAndRememberAsync(tabKey, workspaceId, url, reloadIfSameUrl);
        }

        RenderStreamState(workspaceId);
    }

    // Called when ShowPortView transitions false→true (e.g. switching from a TCP tab back to an HTTP tab).
    private void WakeActiveViewer()
    {
        if (_isClosed || _activeTabKey is null || _activeWorkspaceId is null) return;

        if (_workspaceAuthority.GetValueOrDefault(_activeWorkspaceId, "ai") == "human")
        {
            if (!_webViews.TryGetValue(_activeTabKey, out var webView)) return;
            // Human mode: navigate the direct port WebView to the last known URL if needed.
            if (_lastKnownBrowserUrls.TryGetValue(_activeTabKey, out var lastUrl)
                && Uri.TryCreate(lastUrl, UriKind.Absolute, out var lastUri)
                && !string.Equals(webView.Source?.ToString(), lastUrl, StringComparison.Ordinal))
            {
                var ver = _navigationVersions.GetValueOrDefault(_activeTabKey) + 1;
                _navigationVersions[_activeTabKey] = ver;
                _ = NavigatePortWebViewAsync(_activeTabKey, _activeWorkspaceId, webView, lastUri, ver);
            }
            return;
        }

        // AI mode: RenderStreamState decides visibility + performs navigate/reload as needed.
        RenderStreamState(_activeWorkspaceId);
    }

    private async Task PollHeadlessAddressAsync(string workspaceId)
    {
        if (_workspaceAuthority.GetValueOrDefault(workspaceId, "ai") == "human")
        {
            // In human mode read the URL directly from the native WebView source.
            var humanTabKey = _webViews.Keys
                .FirstOrDefault(k => k.StartsWith($"{workspaceId}:") && k != $"{workspaceId}:viewer");
            if (humanTabKey is not null && _webViews.TryGetValue(humanTabKey, out var humanWv))
            {
                var src = humanWv.Source?.ToString();
                if (!string.IsNullOrWhiteSpace(src))
                {
                    _lastKnownBrowserUrls[humanTabKey] = src;
                    // Don't overwrite the address bar while the user is typing in it.
                    if (DataContext is MainViewModel vm && !AddressBar.IsFocused)
                        vm.UpdateAddressFromBrowser(workspaceId, src);
                }
            }
            return;
        }

        try
        {
            var url = await _serverHttp.GetStringAsync(
                $"/api/browser/current-url/{Uri.EscapeDataString(workspaceId)}");
            if (string.IsNullOrWhiteSpace(url)) return;

            var trimmed = url.Trim();
            var tabKey = $"{workspaceId}:viewer";
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            {
                _lastKnownBrowserUrls[tabKey] = trimmed;
                if (DataContext is MainViewModel vm)
                    vm.UpdateAddressFromBrowser(workspaceId, trimmed);
            }
            else
            {
                _lastKnownBrowserUrls.Remove(tabKey);
                // Chromium is on an error or blank page. Retry navigation to the intended URL
                // so the display recovers automatically once the app is reachable again.
                if (DateTimeOffset.UtcNow - _lastHeadlessRetry >= TimeSpan.FromSeconds(5))
                {
                    var intendedUrl = (DataContext as MainViewModel)?.AddressBarUrl;
                    if (!string.IsNullOrWhiteSpace(intendedUrl)
                        && intendedUrl.StartsWith("http", StringComparison.Ordinal))
                    {
                        _lastHeadlessRetry = DateTimeOffset.UtcNow;
                        _ = PostHeadlessNavigateAsync(workspaceId, intendedUrl, reloadIfSameUrl: true);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }

    private async Task PollControlModeAsync(string workspaceId)
    {
        try
        {
            var json = await _serverHttp.GetStringAsync(
                $"/api/browser/input/control-mode/{Uri.EscapeDataString(workspaceId)}");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var authority = root.GetProperty("authority").GetString() ?? "ai";
            var width = root.GetProperty("width").GetInt32();
            var height = root.GetProperty("height").GetInt32();

            var oldAuthority = _workspaceAuthority.GetValueOrDefault(workspaceId, "ai");
            _workspaceAuthority[workspaceId] = authority;

            if (DataContext is MainViewModel vm)
                vm.Sidebar.ApplyControlMode(workspaceId, authority, width, height);

            if (oldAuthority != authority)
                SwitchWebViewMode(workspaceId, authority);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }

    private void SwitchWebViewMode(string workspaceId, string authority)
    {
        var tutorialVisible = IsTutorialVisible();
        var vm = DataContext as MainViewModel;

        if (authority == "human")
        {
            ChromiumDownloadBanner.IsVisible = false;
            ConnectionLostBanner.IsVisible = false;
            // Activate the direct port WebView; navigate to the last URL the headless browser was at,
            // or fall back to the address bar URL (e.g. when the app is offline and never loaded).
            var viewerTabKey = $"{workspaceId}:viewer";
            if (!_lastKnownBrowserUrls.TryGetValue(viewerTabKey, out var lastUrl))
                lastUrl = vm?.AddressBarUrl;
            if (lastUrl is null) return;
            if (!Uri.TryCreate(lastUrl, UriKind.Absolute, out var lastUri)) return;

            var tabKey = TabKey(workspaceId, lastUri);
            ActivateTab(workspaceId, tabKey, tutorialVisible);

            // Restore human-mode address bar state (prefer where human was last, else AI's URL).
            if (vm is not null)
                vm.AddressBarUrl = _lastKnownBrowserUrls.GetValueOrDefault(tabKey) ?? lastUrl;

            if (_webViews.TryGetValue(tabKey, out var existingWv))
            {
                existingWv.IsVisible = !tutorialVisible;
                return;
            }

            if (!TryGetOrCreateWebView(tabKey, workspaceId, lastUrl, out var wv, out var dest)) return;
            wv.IsVisible = !tutorialVisible;
            var ver = _navigationVersions.GetValueOrDefault(tabKey) + 1;
            _navigationVersions[tabKey] = ver;
            _ = NavigatePortWebViewAsync(tabKey, workspaceId, wv, new Uri(dest), ver);
        }
        else
        {
            // Activate the AI stream viewer tab.
            var tabKey = $"{workspaceId}:viewer";
            ActivateTab(workspaceId, tabKey, tutorialVisible);

            // If the server SSE stream is down, show the connection lost banner instead of the viewer.
            if (_hadBrowserEventsConnection && !_browserEventsConnected)
                ConnectionLostBanner.IsVisible = true;
            else
                WakeActiveViewer();

            // Restore AI-mode address bar state.
            if (vm is not null && _lastKnownBrowserUrls.TryGetValue(tabKey, out var aiUrl))
                vm.AddressBarUrl = aiUrl;
        }
    }

    private void OnPortPaneSizeChanged(object? sender, SizeChangedEventArgs e) { }

    private async Task PostHeadlessNavigateAndRememberAsync(
        string tabKey,
        string workspaceId,
        string url,
        bool reloadIfSameUrl)
    {
        if (await PostHeadlessNavigateAsync(workspaceId, url, reloadIfSameUrl))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_isClosed && _webViews.ContainsKey(tabKey))
                    _lastKnownBrowserUrls[tabKey] = url;
            });
        }
    }

    private async Task<bool> PostHeadlessNavigateAsync(string workspaceId, string url, bool reloadIfSameUrl)
    {
        try
        {
            using var response = await _serverHttp.PostAsync(
                $"/api/browser/navigate/{Uri.EscapeDataString(workspaceId)}?url={Uri.EscapeDataString(url)}&reloadIfSameUrl={(reloadIfSameUrl ? "true" : "false")}",
                null);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Trace.TraceWarning(ex.Message);
            return false;
        }
    }

    private async Task PostHeadlessBrowserCommandAsync(BrowserCommand command, string workspaceId)
    {
        var endpoint = command switch
        {
            BrowserCommand.Back => $"/api/browser/navigate-back/{Uri.EscapeDataString(workspaceId)}",
            BrowserCommand.Forward => $"/api/browser/navigate-forward/{Uri.EscapeDataString(workspaceId)}",
            BrowserCommand.Reload => $"/api/browser/reload/{Uri.EscapeDataString(workspaceId)}",
            _ => null
        };
        if (endpoint is null) return;
        try
        {
            using var response = await _serverHttp.PostAsync(endpoint, null);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }

    private Uri BuildViewerUrl(string workspaceId)
        => new(_serverHttp.BaseAddress!, $"/api/browser/rdp-viewer?workspaceId={Uri.EscapeDataString(workspaceId)}");

    private static bool IsAtViewerUrl(NativeWebView webView, Uri viewerUrl)
        => !ShouldReclaimViewerUrl(webView.Source?.AbsoluteUri, viewerUrl.AbsoluteUri);

    private void RetryViewerNavigation(string tabKey, string workspaceId, NativeWebView webView, Uri viewerUrl)
    {
        RecordWebViewEvent(workspaceId, "viewer_retry", "info", new()
        {
            ["tabKey"] = tabKey,
            ["url"] = viewerUrl.ToString(),
            ["currentSource"] = webView.Source?.ToString() ?? string.Empty,
            ["streamKind"] = _streamStates.GetValueOrDefault(workspaceId)?.Kind.ToString() ?? "unknown",
        });
        Dispatcher.UIThread.Post(
            () =>
            {
                if (CanTouchWebView(tabKey, webView))
                    ReloadWebView(webView, viewerUrl);
            },
            DispatcherPriority.Background);
    }

    private static readonly JsonSerializerOptions _auditJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private void RecordWebViewEvent(string workspaceId, string action, string outcome, Dictionary<string, string> details)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var dto = new { kind = "desktop", source = "webview", action, outcome, workspaceId, details };
                var json = JsonSerializer.Serialize(dto, _auditJsonOptions);
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                await _serverHttp.PostAsync("/api/audit/record", content);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ObjectDisposedException)
            {
                Trace.TraceWarning(ex.Message);
            }
        });
    }


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

// Immutable snapshot of the JS state machine, produced by parsing the JSON returned by
// window.__viewer.snapshot(). Used as Avalonia's only input from the viewer JS layer.
// ObservedAt is UTC-side and lets us tell how stale a snapshot is if polling stops.
internal sealed record ViewerSnapshot(
    string State,
    long Since,
    int FramesReceived,
    long LastFrameAt,
    string WsReadyState,
    string Presence,
    string PageInstanceId,
    bool ServerReportedActive,
    DateTimeOffset ObservedAt)
{
    // Age of the current JS SM state at the moment the snapshot was taken.
    // JS reports `since` as Date.now() ms. We derive age from the JS-side clock so it's
    // immune to clock skew between JS and Avalonia.
    public TimeSpan StateAge { get; init; } = TimeSpan.FromMilliseconds(Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Since));

    public static ViewerSnapshot? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "null") return null;
        try
        {
            // NativeWebView.InvokeScript often wraps the JSON in quotes (returning a
            // literal string result). Handle both a bare JSON object and a JSON string
            // containing JSON.
            var text = raw.TrimStart();
            if (text.StartsWith('"'))
                text = System.Text.Json.JsonSerializer.Deserialize<string>(text) ?? "";
            if (string.IsNullOrWhiteSpace(text) || text == "null") return null;
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            var root = doc.RootElement;
            var since = root.TryGetProperty("since", out var sinceEl) ? sinceEl.GetInt64() : 0L;
            return new ViewerSnapshot(
                State: root.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "",
                Since: since,
                FramesReceived: root.TryGetProperty("framesReceived", out var fr) ? fr.GetInt32() : 0,
                LastFrameAt: root.TryGetProperty("lastFrameAt", out var lfa) ? lfa.GetInt64() : 0L,
                WsReadyState: root.TryGetProperty("wsReadyState", out var ws) ? ws.GetString() ?? "" : "",
                Presence: root.TryGetProperty("presence", out var p) ? p.GetString() ?? "" : "",
                PageInstanceId: root.TryGetProperty("pageInstanceId", out var pid) ? pid.GetString() ?? "" : "",
                ServerReportedActive: root.TryGetProperty("serverReportedActive", out var sra) && sra.ValueKind == System.Text.Json.JsonValueKind.True,
                ObservedAt: DateTimeOffset.UtcNow);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
